using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Api.Services;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Exceptions;
using QuizArena.Identity.Domain.Repositories;
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

    [Theory]
    [InlineData("PUT", "conflict", HttpStatusCode.Conflict)]
    [InlineData("PUT", "not-found", HttpStatusCode.NotFound)]
    [InlineData("PUT", "corrupt", HttpStatusCode.InternalServerError)]
    [InlineData("PUT", "outcome-unknown", HttpStatusCode.InternalServerError)]
    [InlineData("PUT", "unexpected", HttpStatusCode.InternalServerError)]
    [InlineData("PUT", "timeout", HttpStatusCode.ServiceUnavailable)]
    [InlineData("PUT", "sql-timeout", HttpStatusCode.ServiceUnavailable)]
    [InlineData("GET", "sql-throttled", HttpStatusCode.ServiceUnavailable)]
    [InlineData("GET", "corrupt", HttpStatusCode.InternalServerError)]
    [InlineData("GET", "unexpected", HttpStatusCode.InternalServerError)]
    [InlineData("PUT", "unexpected-wrapping-domain-wrapping-transient", HttpStatusCode.InternalServerError)]
    [InlineData("GET", "unexpected-wrapping-domain-wrapping-transient", HttpStatusCode.InternalServerError)]
    public async Task PictureRoutes_WhenApplicationFails_ReturnSafeContractProblem(string method, string scenario, HttpStatusCode expected)
    {
        // Arrange: domain outcomes wrap a transient SQL cause; it must not turn them into 503 (#144).
        Exception error = scenario switch
        {
            "conflict" => new ProfilePictureConflictException(SqlFailure(-2)),
            "not-found" => new PlayerProfileNotFoundException(SqlFailure(40613)),
            "corrupt" => new ProfilePictureCorruptException(new InvalidProfileException("content", Sentinel)),
            "outcome-unknown" => new ProfilePictureCommitOutcomeUnknownException(SqlFailure(-2)),
            "unexpected" => new InvalidOperationException(Sentinel),
            // The 503 walk stops at the domain exception, so the transient SQL error beneath it never yields 503.
            "unexpected-wrapping-domain-wrapping-transient" =>
                new InvalidOperationException(Sentinel, new ProfilePictureConflictException(SqlFailure(-2))),
            "timeout" => new TimeoutException(Sentinel),
            "sql-timeout" => SqlFailure(-2),
            "sql-throttled" => SqlFailure(40613),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        // Act
        using var response = await SendPictureAsync(error, new HttpMethod(method));

        // Assert
        await AssertSafeProblemAsync(response, expected);
        await AssertContractProblemFieldsAsync(response);
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.NoStore.Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        if (scenario == "outcome-unknown")
        {
            (await response.Content.ReadAsStringAsync()).Should().Contain("Reload your profile before retrying");
        }
    }

    [Fact]
    public async Task PutPicture_WhenStoreFailsTransiently_Returns503AndKeepsPreviousPicture()
    {
        // Arrange: upload v1 through the normal host, then replace through a host whose store write times out.
        var subject = $"player-{Guid.NewGuid():N}";
        using (var client = _factory.CreateClient())
        {
            (await SendRealAsync(client, HttpMethod.Put, "/api/users/me", subject, Json("""{ "displayName": "Alex" }"""))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await SendRealAsync(client, HttpMethod.Put, "/api/users/me/picture", subject, Png(PictureSamples.Png))).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await using var flaky = WithFlakyStore(failInfo: false, failReplace: true);
        using var flakyClient = flaky.CreateClient();

        // Act
        using var response = await SendRealAsync(flakyClient, HttpMethod.Put, "/api/users/me/picture", subject, Png(PictureSamples.Jpeg, "image/jpeg"));

        // Assert
        await AssertSafeProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        using var normal = _factory.CreateClient();
        using var read = await SendRealAsync(normal, HttpMethod.Get, "/api/users/me/picture", subject, null);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadAsByteArrayAsync()).Should().Equal(PictureSamples.Png);
    }

    [Fact]
    public async Task PutMe_WhenPictureMetadataReadFails_Returns503AndKeepsPersistedName()
    {
        // Arrange: #144 review: the metadata read happens before the rename, so a failure leaves the name unchanged.
        var subject = $"player-{Guid.NewGuid():N}";
        using (var client = _factory.CreateClient())
        {
            (await SendRealAsync(client, HttpMethod.Put, "/api/users/me", subject, Json("""{ "displayName": "Alex" }"""))).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await using var flaky = WithFlakyStore(failInfo: true, failReplace: false);
        using var flakyClient = flaky.CreateClient();

        // Act
        using var response = await SendRealAsync(flakyClient, HttpMethod.Put, "/api/users/me", subject, Json("""{ "displayName": "Sam" }"""));

        // Assert
        await AssertSafeProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        await using var db = _factory.NewDbContext();
        (await db.UserProfiles.SingleAsync(profile => profile.Subject == subject)).DisplayName.Should().Be("Alex");
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

    private async Task<HttpResponseMessage> SendPictureAsync(Exception error, HttpMethod method)
    {
        await using var failing = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProfileApplication>();
            services.AddSingleton<IProfileApplication>(new FailingApplication(error));
        }));
        using var client = failing.CreateClient();
        var response = await SendRealAsync(client, method, "/api/users/me/picture", $"player-{Guid.NewGuid():N}",
            method == HttpMethod.Put ? Png(PictureSamples.Png) : null);
        return response;
    }

    private static async Task<HttpResponseMessage> SendRealAsync(HttpClient client, HttpMethod method, string path, string subject, HttpContent? content)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        var token = TestTokens.Create(ActorKind.User, "profiles.read profiles.write", claims => claims["sub"] = subject);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        await response.Content.LoadIntoBufferAsync();
        return response;
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static ByteArrayContent Png(byte[] bytes, string contentType = "image/png")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    // Contract Problem schema: exactly type, title, status, detail and traceId.
    private static async Task AssertContractProblemFieldsAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("type", "title", "status", "detail", "traceId");
        document.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // The real store, except that the chosen operation fails with a transient SQL timeout (reads otherwise delegate).
    private WebApplicationFactory<QuizArena.Identity.Api.Program> WithFlakyStore(bool failInfo, bool failReplace) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProfilePictureStore>();
            services.AddScoped<IProfilePictureStore>(provider => new FlakyPictureStore(
                ActivatorUtilities.CreateInstance<QuizArena.Identity.Data.Repositories.ProfilePictureStore>(provider), failInfo, failReplace));
        }));

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

        public Task<ProfileResponse> ReplaceMyPictureAsync(AuthenticatedActor actor, string contentType, byte[] content, CancellationToken cancellationToken) => Task.FromException<ProfileResponse>(error);
    }

    private sealed class FlakyPictureStore(IProfilePictureStore inner, bool failInfo, bool failReplace) : IProfilePictureStore
    {
        public Task<ProfilePictureInfo?> GetInfoAsync(Guid profileId, CancellationToken cancellationToken) =>
            failInfo ? Task.FromException<ProfilePictureInfo?>(SqlFailure(-2)) : inner.GetInfoAsync(profileId, cancellationToken);

        public Task<ProfilePicture?> GetAsync(Guid profileId, CancellationToken cancellationToken) => inner.GetAsync(profileId, cancellationToken);

        public Task<ProfilePictureInfo> ReplaceAsync(Guid profileId, string contentType, byte[] content, DateTime updatedAt, CancellationToken cancellationToken) =>
            failReplace ? Task.FromException<ProfilePictureInfo>(SqlFailure(-2)) : inner.ReplaceAsync(profileId, contentType, content, updatedAt, cancellationToken);

        public Task<bool> DeleteAsync(Guid profileId, CancellationToken cancellationToken) => inner.DeleteAsync(profileId, cancellationToken);
    }
}
