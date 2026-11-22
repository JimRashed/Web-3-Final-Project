using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Api.Services;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Endpoints;

/// <summary>
/// DR-3: transient database failures on Identity return 503 problem+json without exception details;
/// other failures keep the generic 500 fallback.
/// </summary>
public sealed class ProfileErrorTests : IClassFixture<IdentityApiFactory>
{
    private const string Sentinel = "SECRET-token-SQL-parser-details";

    private readonly IdentityApiFactory _factory;

    public ProfileErrorTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("sql-timeout")]
    [InlineData("sql-connection")]
    [InlineData("timeout")]
    [InlineData("wrapped-connection")]
    public async Task GetMe_WhenDependencyUnavailable_Returns503Problem(string scenario)
    {
        // Arrange
        var error = Failure(scenario);

        // Act
        using var response = await SendAsync(error, HttpMethod.Get, null);

        // Assert
        await AssertSafeProblemAsync(response, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task PutMe_WhenDependencyUnavailable_Returns503Problem()
    {
        // Act
        using var response = await SendAsync(Failure("sql-connection"), HttpMethod.Put, """{ "displayName": "Alex" }""");

        // Assert
        await AssertSafeProblemAsync(response, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetMe_WhenNonTransientSqlError_Returns500()
    {
        // Act
        using var response = await SendAsync(SqlFailure(2601), HttpMethod.Get, null);

        // Assert
        await AssertSafeProblemAsync(response, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetMe_WhenUnexpectedError_Returns500()
    {
        // Act
        using var response = await SendAsync(new InvalidOperationException(Sentinel), HttpMethod.Get, null);

        // Assert
        await AssertSafeProblemAsync(response, HttpStatusCode.InternalServerError);
    }

    private static Exception Failure(string scenario) => scenario switch
    {
        "sql-timeout" => SqlFailure(-2),
        "sql-connection" => SqlFailure(53),
        "timeout" => new TimeoutException(Sentinel),
        "wrapped-connection" => new DbUpdateException(Sentinel, SqlFailure(53)),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
    };

    private async Task<HttpResponseMessage> SendAsync(Exception error, HttpMethod method, string? body)
    {
        await using var failing = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProfileApplication>();
            services.AddSingleton<IProfileApplication>(new FailingApplication(error));
        }));
        using var client = failing.CreateClient();
        using var request = new HttpRequestMessage(method, "/api/users/me");
        var token = TestTokens.Create(ActorKind.User, "profiles.read profiles.write", claims => claims["sub"] = $"player-{Guid.NewGuid():N}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request);
        await response.Content.LoadIntoBufferAsync();
        return response;
    }

    private static async Task AssertSafeProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().NotContain(Sentinel).And.NotContain("Exception");
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

    private sealed class FailingApplication(Exception error) : IProfileApplication
    {
        public Task<ProfileResponse?> GetMeAsync(AuthenticatedActor actor, CancellationToken cancellationToken) => Task.FromException<ProfileResponse?>(error);

        public Task<ProfileResponse> UpsertMeAsync(AuthenticatedActor actor, UpdateProfileRequest request, CancellationToken cancellationToken) => Task.FromException<ProfileResponse>(error);

        public Task<ProfilePicture?> GetMyPictureAsync(AuthenticatedActor actor, CancellationToken cancellationToken) => Task.FromException<ProfilePicture?>(error);

        public Task<ProfilePictureInfo> ReplaceMyPictureAsync(AuthenticatedActor actor, string contentType, byte[] content, CancellationToken cancellationToken) => Task.FromException<ProfilePictureInfo>(error);
    }
}
