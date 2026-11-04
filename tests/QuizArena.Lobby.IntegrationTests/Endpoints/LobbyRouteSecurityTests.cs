using System.Net;
using FluentAssertions;
using QuizArena.Lobby.Api.Security;
using QuizArena.Lobby.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Endpoints;

public sealed class LobbyRouteSecurityTests(LobbyApiFixture fixture) : IClassFixture<LobbyApiFixture>
{
    public static IEnumerable<object[]> RejectedTokens() =>
        from route in new[] { "create", "preview", "join", "lock", "verify" }
        from scenario in new[] { "missing", "malformed", "bad-signature", "unknown-key", "expired", "no-expiry", "wrong-issuer", "wrong-audience", "id-token", "no-scope", "wrong-scope", "prefix", "suffix", "wrong-client", "wrong-actor", "wrong-actor-token" }
        select new object[] { route, scenario };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task ActualRoute_InvalidTokenOrGrant_DeniesBeforeMutation(string route, string scenario)
    {
        // Arrange
        var lobby = await fixture.SeedAsync();
        var (method, path, scope, actor) = Route(route, lobby.JoinCode);
        var other = actor == ActorKind.User ? ActorKind.Machine : ActorKind.User;
        string? token = scenario switch
        {
            "missing" => null,
            "malformed" => "invalid-jwt",
            "bad-signature" => TestTokens.Create(actor, scope, key: TestTokens.ImpostorKey),
            "unknown-key" => TestTokens.Create(actor, scope, key: TestTokens.UnknownKey),
            "expired" => TestTokens.Create(actor, scope, expires: DateTime.UtcNow.AddMinutes(-5)),
            "no-expiry" => TestTokens.Create(actor, scope, includeExpiry: false),
            "wrong-issuer" => TestTokens.Create(actor, scope, claims => claims["iss"] = "https://wrong.example"),
            "wrong-audience" => TestTokens.Create(actor, scope, claims => claims["aud"] = "wrong-audience"),
            "id-token" => TestTokens.Create(actor, scope, claims => claims["typ"] = "ID"),
            "no-scope" => TestTokens.Create(actor, "openid"),
            "wrong-scope" => TestTokens.Create(actor, "unrelated.scope"),
            "prefix" => TestTokens.Create(actor, scope[..^1]),
            "suffix" => TestTokens.Create(actor, scope + "x"),
            "wrong-client" => TestTokens.Create(actor, scope, claims => claims["azp"] = "untrusted-client"),
            "wrong-actor" => TestTokens.Create(actor, scope, claims => claims["qa_actor"] = other == ActorKind.User ? "user" : "machine"),
            _ => TestTokens.Create(other, scope),
        };
        var unauthorized = scenario is "missing" or "malformed" or "bad-signature" or "unknown-key" or "expired" or "no-expiry" or "wrong-issuer" or "wrong-audience" or "id-token";

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, method, path, token, route is "create" or "join" ? "{}" : null);

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, unauthorized ? 401 : 403);
        if (unauthorized)
        {
            var challenge = response.Headers.WwwAuthenticate.Should().ContainSingle(value => value.Scheme == "Bearer").Subject;
            challenge.Parameter.Should().Be(scenario == "missing" ? null : "error=\"invalid_token\"");
        }

        var persisted = await fixture.Database.Repository().GetByJoinCodeAsync(lobby.JoinCode);
        persisted!.Players.Should().ContainSingle();
        persisted.Status.ToString().Should().Be("Open");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("preview")]
    [InlineData("join")]
    [InlineData("lock")]
    [InlineData("verify")]
    public async Task ActualRoute_ExactScopeInScpArrayAndAllowedActor_Succeeds(string route)
    {
        // Arrange
        var lobby = await fixture.SeedAsync(full: route == "lock");
        var (method, path, scope, actor) = Route(route, lobby.JoinCode);
        var token = TestTokens.Create(actor, scope, claims =>
        {
            claims["sub"] = route == "join" ? "guest" : "host";
            claims.Remove("scope");
            claims["scp"] = new[] { "openid", scope };
        });

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, method, path, token, route is "create" or "join" ? "{}" : null);

        // Assert
        response.StatusCode.Should().Be(route == "create" ? HttpStatusCode.Created : HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Health_MissingOrInvalidIrrelevantBearer_RemainsAnonymous(bool invalidToken)
    {
        // Arrange
        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Get, "/health", invalidToken ? "not-a-token" : null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("{\"status\":\"ok\"}");
    }

    private static (HttpMethod Method, string Path, string Scope, ActorKind Actor) Route(string route, string code) =>
        route == "create" ? (HttpMethod.Post, "/api/lobbies", LobbyPolicies.LobbiesWrite, ActorKind.User) : LobbyInputTests.Route(route, code);
}
