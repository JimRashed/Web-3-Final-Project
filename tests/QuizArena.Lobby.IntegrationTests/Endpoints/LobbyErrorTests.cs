using System.Diagnostics;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizArena.Lobby.Api.Contracts;
using QuizArena.Lobby.Api.Errors;
using QuizArena.Lobby.Api.Services;
using QuizArena.Lobby.Data.Repositories;
using QuizArena.Lobby.Domain.Exceptions;
using QuizArena.Lobby.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Endpoints;

public sealed class LobbyErrorTests
{
    private const string Sentinel = "SECRET-token-SQL-parser-details";

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(256)]
    public void Problem_TraceIdentifierWithoutActivity_RespectsCommitted100CharacterLimit(int identifierLength)
    {
        // Arrange
        var previousActivity = Activity.Current;
        Activity.Current = null;
        var identifier = new string('a', identifierLength);
        var context = new DefaultHttpContext { TraceIdentifier = identifier };
        var problem = new ProblemDetails { Status = 400 };
        try
        {
            // Act
            LobbyProblems.Customize(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });

            // Assert
            problem.Extensions["traceId"].Should().Be(identifier[..100]);
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }

    [Theory]
    [InlineData("invalid", 400)]
    [InlineData("unauthorized", 401)]
    [InlineData("forbidden", 403)]
    [InlineData("conflict", 409)]
    [InlineData("persistence", 503)]
    [InlineData("timeout", 503)]
    [InlineData("sql-timeout", 503)]
    [InlineData("sql-connection", 503)]
    [InlineData("wrapped-connection", 503)]
    [InlineData("sql-constraint", 500)]
    [InlineData("update", 500)]
    [InlineData("unexpected", 500)]
    public async Task ExceptionMiddleware_KnownAndUnexpectedFailures_ReturnsOnlySafeProblem(string scenario, int status)
    {
        // Arrange
        Exception error = scenario switch
        {
            "invalid" => new InvalidLobbyException("secret-field", Sentinel),
            "unauthorized" => new UnauthorizedAccessException(Sentinel),
            "forbidden" => new LobbyForbiddenException(Sentinel),
            "conflict" => new LobbyConflictException(Sentinel),
            "persistence" => new LobbyPersistenceException(Sentinel, new InvalidOperationException(Sentinel)),
            "timeout" => new TimeoutException(Sentinel),
            "sql-timeout" => SqlFailure(-2),
            "sql-connection" => SqlFailure(53),
            "wrapped-connection" => new DbUpdateException(Sentinel, SqlFailure(53)),
            "sql-constraint" => SqlFailure(2601),
            "update" => new DbUpdateException(Sentinel),
            _ => new InvalidOperationException(Sentinel),
        };
        await using var factory = new SecuredApiFactory();
        await using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILobbyService>();
            services.AddSingleton<ILobbyService>(new FailureService(_ => Task.FromException(error)));
        }));
        using var client = failing.CreateClient();

        // Act
        using var response = await LobbyApiFixture.SendAsync(client, HttpMethod.Post, "/api/lobbies", LobbyApiFixture.Token(), "{}");

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, status);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(Sentinel).And.NotContain("secret-field").And.NotContain("Exception");
        if (status == 401)
        {
            response.Headers.WwwAuthenticate.Should().ContainSingle(value => value.Scheme == "Bearer");
        }
    }

    [Fact]
    public async Task Request_CancelledInFlight_PropagatesCancellationWithoutSynthetic500()
    {
        // Arrange
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = new SecuredApiFactory();
        await using var waiting = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILobbyService>();
            services.AddSingleton<ILobbyService>(new FailureService(async token =>
            {
                entered.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                catch (OperationCanceledException)
                {
                    observed.TrySetResult(true);
                    throw;
                }
            }));
        }));
        using var client = waiting.CreateClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var request = LobbyApiFixture.SendAsync(client, HttpMethod.Post, "/api/lobbies", LobbyApiFixture.Token(), "{}", cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Act
        await cancellation.CancelAsync();

        // Assert
        Func<Task> act = () => request;
        await act.Should().ThrowAsync<OperationCanceledException>();
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ExceptionHandler_ResponseAlreadyStarted_DoesNotWriteAgain()
    {
        // Arrange
        await using var factory = new SecuredApiFactory();
        using var client = factory.CreateClient();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponse());
        var handler = new LobbyExceptionHandler(factory.Services.GetRequiredService<IProblemDetailsService>());

        // Act
        var handled = await handler.TryHandleAsync(context, new InvalidOperationException(Sentinel), CancellationToken.None);

        // Assert
        handled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(200);
    }

    private static SqlException SqlFailure(int number)
    {
        // Construct real provider exception objects without network I/O or an unreachable dependency.
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First();
        var arguments = constructor.GetParameters().Select(parameter => parameter.ParameterType == typeof(int) ? (object)(parameter.Position == 0 ? number : 0)
            : parameter.ParameterType == typeof(byte) ? (byte)0
            : parameter.ParameterType == typeof(uint) ? (uint)0
            : parameter.ParameterType == typeof(string) ? Sentinel : null).ToArray();
        var error = (SqlError)constructor.Invoke(arguments);
        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(collection, [error]);
        var create = typeof(SqlException).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(method =>
            method.Name == "CreateException" && method.GetParameters().Length == 2
            && method.GetParameters()[0].ParameterType == typeof(SqlErrorCollection));
        return (SqlException)create.Invoke(null, [collection, "test-server-version"])!;
    }

    private sealed class StartedResponse : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class FailureService(Func<CancellationToken, Task> failure) : ILobbyService
    {
        public Task<LobbyResponse> CreateAsync(System.Security.Claims.ClaimsPrincipal principal, CancellationToken cancellationToken = default) => FailAsync<LobbyResponse>(cancellationToken);
        public Task<LobbyResponse?> PreviewAsync(System.Security.Claims.ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default) => FailAsync<LobbyResponse?>(cancellationToken);
        public Task<LobbyResponse?> JoinAsync(System.Security.Claims.ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default) => FailAsync<LobbyResponse?>(cancellationToken);
        public Task<LobbyResponse?> LockAsync(System.Security.Claims.ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default) => FailAsync<LobbyResponse?>(cancellationToken);
        public Task<LobbyProvisionResponse?> VerifyAsync(System.Security.Claims.ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default) => FailAsync<LobbyProvisionResponse?>(cancellationToken);

        private async Task<T> FailAsync<T>(CancellationToken cancellationToken)
        {
            await failure(cancellationToken);
            return default!;
        }
    }
}
