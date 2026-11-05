using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Endpoints;

/// <summary>HTTP tests for /api/users/me and the retired routes, through the real pipeline and database.</summary>
public sealed class ProfileEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public ProfileEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    private static string NewSubject() => $"player-{Guid.NewGuid():N}";

    private static string Token(string subject, string scope = "profiles.read profiles.write") =>
        TestTokens.Create(ActorKind.User, scope, claims => claims["sub"] = subject);

    [Fact]
    public async Task GetMe_WithoutProfile_Returns404Problem()
    {
        // Act
        using var response = await Send(HttpMethod.Get, "/api/users/me", Token(NewSubject()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task PutMe_ThenGetMe_ReturnsProfileForTokenIdentity()
    {
        // Arrange
        var subject = NewSubject();

        // Act
        using var put = await Send(HttpMethod.Put, "/api/users/me", Token(subject), """{ "displayName": "  Alex  " }""");
        using var get = await Send(HttpMethod.Get, "/api/users/me", Token(subject));

        // Assert
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await Json(get);
        profile.GetProperty("issuer").GetString().Should().Be(TestTokens.Issuer);
        profile.GetProperty("subject").GetString().Should().Be(subject);
        profile.GetProperty("displayName").GetString().Should().Be("Alex");
        profile.GetProperty("id").GetGuid().Should().Be((await Json(put)).GetProperty("id").GetGuid());
        profile.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "issuer", "subject", "displayName");
    }

    [Fact]
    public async Task PutMe_Twice_RenamesSameProfileAndPersists()
    {
        // Arrange
        var subject = NewSubject();
        using var first = await Send(HttpMethod.Put, "/api/users/me", Token(subject), """{ "displayName": "Alex" }""");

        // Act
        using var second = await Send(HttpMethod.Put, "/api/users/me", Token(subject), """{ "displayName": "Sam" }""");

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(second)).GetProperty("id").GetGuid().Should().Be((await Json(first)).GetProperty("id").GetGuid());
        await using var db = _factory.NewDbContext();
        var stored = await db.UserProfiles.SingleAsync(p => p.Subject == subject);
        stored.DisplayName.Should().Be("Sam");
    }

    [Theory]
    [InlineData("""{ "displayName": "   " }""", "displayName")]
    [InlineData("""{ "displayName": "" }""", "displayName")]
    [InlineData("""{ }""", "displayName")]
    public async Task PutMe_WithInvalidDisplayName_Returns400WithFieldError(string body, string field)
    {
        // Act
        using var response = await Send(HttpMethod.Put, "/api/users/me", Token(NewSubject()), body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await Json(response)).GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();
    }

    [Fact]
    public async Task PutMe_WithTooLongDisplayName_Returns400()
    {
        // Act
        using var response = await Send(HttpMethod.Put, "/api/users/me", Token(NewSubject()), JsonSerializer.Serialize(new { displayName = new string('a', 81) }));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("""{ "displayName": "Alex", "userId": "someone-else" }""")]
    [InlineData("""{ "displayName": "Alex", "subject": "someone-else" }""")]
    [InlineData("""{ "displayName": "Alex" """)]
    public async Task PutMe_WithSpoofedFieldsOrMalformedJson_Returns400AndCreatesNothing(string body)
    {
        // Arrange
        var subject = NewSubject();

        // Act
        using var response = await Send(HttpMethod.Put, "/api/users/me", Token(subject), body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var db = _factory.NewDbContext();
        (await db.UserProfiles.AnyAsync(p => p.Subject == subject)).Should().BeFalse();
    }

    [Fact]
    public async Task OtherPlayer_CannotReadOrChangeSomeoneElsesProfile()
    {
        // Arrange
        var alice = NewSubject();
        var bob = NewSubject();
        using var created = await Send(HttpMethod.Put, "/api/users/me", Token(alice), """{ "displayName": "Alice" }""");

        // Act
        using var bobReads = await Send(HttpMethod.Get, "/api/users/me", Token(bob));
        using var bobWrites = await Send(HttpMethod.Put, "/api/users/me", Token(bob), """{ "displayName": "Hacked" }""");

        // Assert
        bobReads.StatusCode.Should().Be(HttpStatusCode.NotFound);
        bobWrites.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(bobWrites)).GetProperty("subject").GetString().Should().Be(bob);
        await using var db = _factory.NewDbContext();
        (await db.UserProfiles.SingleAsync(p => p.Subject == alice)).DisplayName.Should().Be("Alice");
    }

    [Theory]
    [InlineData("GET", "profiles.write")]
    [InlineData("PUT", "profiles.read")]
    public async Task MeEndpoints_WithOnlyTheOtherScope_Return403(string method, string scope)
    {
        // Act
        using var response = await Send(new HttpMethod(method), "/api/users/me", Token(NewSubject(), scope), method == "PUT" ? """{ "displayName": "Alex" }""" : null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MeEndpoints_WithMachineTokenOrNoToken_AreRejected()
    {
        // Arrange
        var machine = TestTokens.Create(ActorKind.Machine, "profiles.read profiles.write");

        // Act
        using var withMachine = await Send(HttpMethod.Get, "/api/users/me", machine);
        using var withoutToken = await Send(HttpMethod.Get, "/api/users/me", null);

        // Assert
        withMachine.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        withoutToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST", "/api/auth/register", """{ "username": "alex", "password": "Secret123!" }""")]
    [InlineData("POST", "/api/auth/login", """{ "username": "alex", "password": "Secret123!" }""")]
    [InlineData("POST", "/api/auth/login", "not json at all")]
    [InlineData("POST", "/api/auth/register", null)]
    [InlineData("GET", "/api/users/8f7d2a15-2e7a-4a7d-8c3a-0f4e4d3f9a21", null)]
    public async Task RetiredRoutes_WithoutTokenAndAnyBody_Return410WithOidcGuidance(string method, string path, string? body)
    {
        // Act
        using var response = await Send(new HttpMethod(method), path, token: null, body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await Json(response);
        problem.GetProperty("status").GetInt32().Should().Be(410);
        problem.GetProperty("detail").GetString().Should().Contain("OIDC");
        problem.ToString().Should().NotContain("Secret123!");
    }

    [Fact]
    public async Task RetiredRoute_WithInvalidBearerHeader_StillReturns410()
    {
        // Act
        using var response = await Send(HttpMethod.Post, "/api/auth/login", "not-a-jwt", """{ "password": "x" }""");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task OpenApiDocument_InDevelopment_ListsExactlyTheContractOperations()
    {
        // Act
        using var response = await Send(HttpMethod.Get, "/openapi/v1.json", token: null);
        var generated = Operations(await Json(response));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        generated.Should().BeEquivalentTo(Operations(ReadContract()));
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, string? body = null)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<string> Operations(JsonElement openApi) =>
        openApi.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(op => op.Name is "get" or "put" or "post" or "delete" or "patch")
                .Select(op => $"{op.Name.ToUpperInvariant()} {path.Name.Replace("{id}", "{param}", StringComparison.Ordinal)}"))
            .ToList();

    private static JsonElement ReadContract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
        {
            directory = directory.Parent;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "contracts", "openapi", "identity.yaml")));
        return document.RootElement.Clone();
    }
}
