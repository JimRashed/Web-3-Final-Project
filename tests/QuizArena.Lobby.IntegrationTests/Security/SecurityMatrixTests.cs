using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using QuizArena.Lobby.Api.Security;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Security;

public sealed class SecurityMatrixTests : IClassFixture<SecuredApiFactory>
{
    private const string AllowedOrigin = "http://localhost:5173";

    private readonly SecuredApiFactory _factory;

    public SecurityMatrixTests(SecuredApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string> Policies => new(LobbyPolicies.All.Keys);

    public static TheoryData<string, string> UnauthenticatedCases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var policy in LobbyPolicies.All.Keys)
            {
                foreach (var kind in new[] { "missing", "malformed", "bad-signature", "unknown-key", "expired", "no-expiry", "wrong-issuer", "wrong-audience", "id-token-type" })
                {
                    data.Add(policy, kind);
                }
            }

            return data;
        }
    }

    public static TheoryData<string, string> ForbiddenCases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var policy in LobbyPolicies.All.Keys)
            {
                foreach (var kind in new[] { "no-scope", "other-scope", "scope-suffix", "scope-prefix", "other-actor-claim", "other-client", "other-actor-token" })
                {
                    data.Add(policy, kind);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Policies))]
    public async Task PolicyEndpoint_WithRequiredScopeAndActor_Returns200(string policy)
    {
        // Arrange
        var token = TestTokens.Create(LobbyPolicies.All[policy], $"openid profile {policy}");

        // Act
        using var response = await GetAsync(policy, token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(Policies))]
    public async Task PolicyEndpoint_WithScopeInScpArray_Returns200(string policy)
    {
        // Arrange
        var token = TestTokens.Create(LobbyPolicies.All[policy], policy, claims =>
        {
            claims.Remove("scope");
            claims["scp"] = new[] { "openid", policy };
        });

        // Act
        using var response = await GetAsync(policy, token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(UnauthenticatedCases))]
    public async Task PolicyEndpoint_WithMissingOrInvalidToken_Returns401Problem(string policy, string kind)
    {
        // Arrange
        var actor = LobbyPolicies.All[policy];
        string? token = kind switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "bad-signature" => TestTokens.Create(actor, policy, key: TestTokens.ImpostorKey),
            "unknown-key" => TestTokens.Create(actor, policy, key: TestTokens.UnknownKey),
            "expired" => TestTokens.Create(actor, policy, expires: DateTime.UtcNow.AddMinutes(-5)),
            "no-expiry" => TestTokens.Create(actor, policy, includeExpiry: false),
            "wrong-issuer" => TestTokens.Create(actor, policy, claims => claims["iss"] = "http://localhost:8080/realms/other"),
            "wrong-audience" => TestTokens.Create(actor, policy, claims => claims["aud"] = "quizarena-other"),
            "id-token-type" => TestTokens.Create(actor, policy, claims => claims["typ"] = "ID"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // Act
        using var response = await GetAsync(policy, token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().Contain(header => header.Scheme == "Bearer");
        await AssertProblemAsync(response, 401);
    }

    [Theory]
    [MemberData(nameof(ForbiddenCases))]
    public async Task PolicyEndpoint_WithValidTokenLackingGrant_Returns403Problem(string policy, string kind)
    {
        // Arrange
        var actor = LobbyPolicies.All[policy];
        var otherActor = actor == ActorKind.User ? ActorKind.Machine : ActorKind.User;
        var token = kind switch
        {
            "no-scope" => TestTokens.Create(actor, "openid profile"),
            "other-scope" => TestTokens.Create(actor, "openid unrelated.read"),
            "scope-suffix" => TestTokens.Create(actor, $"{policy}x"),
            "scope-prefix" => TestTokens.Create(actor, policy[..^1]),
            "other-actor-claim" => TestTokens.Create(actor, policy, claims => claims["qa_actor"] = otherActor == ActorKind.User ? "user" : "machine"),
            "other-client" => TestTokens.Create(actor, policy, claims => claims["azp"] = otherActor == ActorKind.User ? TestTokens.BrowserClient : TestTokens.MachineClient),
            "other-actor-token" => TestTokens.Create(otherActor, policy),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // Act
        using var response = await GetAsync(policy, token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemAsync(response, 403);
    }

    [Fact]
    public async Task PolicyEndpoint_WithSpoofedUserIdQueryAndHeader_UsesTokenSubject()
    {
        // Arrange
        var (policy, actor) = LobbyPolicies.All.First();
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/__security-probe/{policy}?userId=player-b");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(actor, policy));
        request.Headers.Add("X-User-Id", "player-b");

        // Act
        using var response = await client.SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var expected = actor == ActorKind.User ? TestTokens.UserSubject : TestTokens.MachineSubject;
        body.RootElement.GetProperty("subject").GetString().Should().Be(expected);
        body.RootElement.GetProperty("issuer").GetString().Should().Be(TestTokens.Issuer);
    }

    [Fact]
    public async Task UnattributedEndpoint_WithoutToken_FallbackPolicyReturns401()
    {
        // Act
        using var response = await GetAsync("authenticated", token: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnattributedEndpoint_WithAnyValidAccessToken_Returns200()
    {
        // Act
        using var response = await GetAsync("authenticated", TestTokens.Create(ActorKind.User, "openid"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_WithInvalidBearerHeader_StillReturns200()
    {
        // Arrange
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        // Act
        using var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Preflight_FromConfiguredFrontendOrigin_IsAllowedWithoutToken()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = Preflight(AllowedOrigin);

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(AllowedOrigin);
    }

    [Fact]
    public async Task Preflight_FromUnknownOrigin_GetsNoCorsGrant()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = Preflight("http://evil.invalid");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/__security-probe/authenticated");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return request;
    }

    private async Task<HttpResponseMessage> GetAsync(string route, string? token)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/__security-probe/{route}");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status)
    {
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetInt32().Should().Be(status);
        body.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }
}
