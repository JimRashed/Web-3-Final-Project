using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Statistics.Api.Security;
using QuizArena.Statistics.IntegrationTests.Security;
using Xunit;
using static QuizArena.Statistics.IntegrationTests.Endpoints.StatisticsHttp;

namespace QuizArena.Statistics.IntegrationTests.Endpoints;

/// <summary>
/// The full bearer rejection and grant matrix against every real Statistics route (the security matrix tests only
/// cover a probe controller). Rejected ingestion must persist nothing.
/// </summary>
public sealed class StatisticsRouteSecurityTests : IClassFixture<StatisticsApiFactory>
{
    private const string PlayerSubject = "player-route-security";
    private readonly StatisticsApiFactory _factory;

    public StatisticsRouteSecurityTests(StatisticsApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Route key → (method, path, required scope, actor).</summary>
    private static readonly Dictionary<string, (HttpMethod Method, string Path, string Scope, ActorKind Actor)> Routes = new()
    {
        ["record"] = (HttpMethod.Post, "/api/results", "results.write", ActorKind.Machine),
        ["stats"] = (HttpMethod.Get, $"/api/players/{PlayerSubject}/stats", "stats.read", ActorKind.User),
        ["history"] = (HttpMethod.Get, $"/api/players/{PlayerSubject}/matches", "stats.read", ActorKind.User),
        ["leaderboard"] = (HttpMethod.Get, "/api/leaderboard", "leaderboard.read", ActorKind.User),
    };

    public static TheoryData<string, string> UnauthenticatedCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var route in Routes.Keys)
        {
            foreach (var kind in new[] { "missing", "malformed", "bad-signature", "unknown-key", "expired", "no-expiry", "wrong-issuer", "wrong-audience", "id-token-type" })
            {
                data.Add(route, kind);
            }
        }

        return data;
    }

    public static TheoryData<string, string> ForbiddenCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var route in Routes.Keys)
        {
            foreach (var kind in new[] { "no-scope", "scope-prefix", "scope-suffix", "no-subject", "other-client", "no-actor-claim", "other-actor-claim", "other-actor-token" })
            {
                data.Add(route, kind);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(UnauthenticatedCases))]
    public async Task Routes_WithMissingOrInvalidToken_Return401Challenge(string route, string kind)
    {
        // Arrange
        var (method, path, scope, actor) = Routes[route];
        var token = kind switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "bad-signature" => Token(actor, scope, key: TestTokens.ImpostorKey),
            "unknown-key" => Token(actor, scope, key: TestTokens.UnknownKey),
            "expired" => TestTokens.Create(actor, scope, Subject(actor), expires: DateTime.UtcNow.AddMinutes(-5)),
            "no-expiry" => TestTokens.Create(actor, scope, Subject(actor), includeExpiry: false),
            "wrong-issuer" => Token(actor, scope, claims => claims["iss"] = "http://localhost:8080/realms/other"),
            "wrong-audience" => Token(actor, scope, claims => claims["aud"] = "quizarena-identity"),
            "id-token-type" => Token(actor, scope, claims => claims["typ"] = "ID"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var matchId = $"m-{Guid.NewGuid():N}";

        // Act
        using var response = await Send(_factory, method, path, token, Body(method, matchId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var challenge = response.Headers.WwwAuthenticate.Should().ContainSingle(header => header.Scheme == "Bearer").Subject;
        challenge.Parameter.Should().Be(kind == "missing" ? null : "error=\"invalid_token\"");
        await ShouldNotHaveStored(matchId);
    }

    [Theory]
    [MemberData(nameof(ForbiddenCases))]
    public async Task Routes_WithValidTokenLackingGrant_Return403(string route, string kind)
    {
        // Arrange
        var (method, path, scope, actor) = Routes[route];
        var otherActor = actor == ActorKind.User ? ActorKind.Machine : ActorKind.User;
        var token = kind switch
        {
            "no-scope" => Token(actor, "openid profile"),
            "scope-prefix" => Token(actor, scope + "x"),
            "scope-suffix" => Token(actor, "x" + scope),
            "no-subject" => Token(actor, scope, claims => claims.Remove("sub")),
            "other-client" => Token(actor, scope, claims => claims["azp"] = actor == ActorKind.User ? TestTokens.MachineClient : TestTokens.BrowserClient),
            "no-actor-claim" => Token(actor, scope, claims => claims.Remove("qa_actor")),
            "other-actor-claim" => Token(actor, scope, claims => claims["qa_actor"] = actor == ActorKind.User ? "machine" : "user"),
            "other-actor-token" => Token(otherActor, scope),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var matchId = $"m-{Guid.NewGuid():N}";

        // Act
        using var response = await Send(_factory, method, path, token, Body(method, matchId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        await ShouldNotHaveStored(matchId);
    }

    [Theory]
    [InlineData("stats")]
    [InlineData("history")]
    [InlineData("leaderboard")]
    public async Task ReadRoutes_WithExactGrant_Return200(string route)
    {
        // Arrange
        var (method, path, scope, actor) = Routes[route];

        // Act
        using var response = await Send(_factory, method, path, Token(actor, scope));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static Action<Dictionary<string, object>> Subject(ActorKind actor) =>
        claims => claims["sub"] = actor == ActorKind.User ? PlayerSubject : TestTokens.MachineSubject;

    private static string Token(ActorKind actor, string scope, Action<Dictionary<string, object>>? customize = null, Microsoft.IdentityModel.Tokens.SecurityKey? key = null) =>
        TestTokens.Create(actor, scope, claims =>
        {
            Subject(actor)(claims);
            customize?.Invoke(claims);
        }, key);

    private static string? Body(HttpMethod method, string matchId) =>
        method == HttpMethod.Post ? Result(matchId, PlayerSubject, 1, false, "2026-11-04T12:00:00Z") : null;

    private async Task ShouldNotHaveStored(string matchId)
    {
        await using var db = _factory.NewDbContext();
        (await db.MatchResults.AnyAsync(result => result.MatchId == matchId)).Should().BeFalse();
    }
}

/// <summary>Empty collections over HTTP. A separate class gets its own empty database.</summary>
public sealed class StatisticsEmptyCollectionTests : IClassFixture<StatisticsApiFactory>
{
    private readonly StatisticsApiFactory _factory;

    public StatisticsEmptyCollectionTests(StatisticsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reads_OnEmptyDatabase_ReturnEmptyArraysAndZeroTotals()
    {
        // Act
        using var leaderboard = await Send(_factory, HttpMethod.Get, "/api/leaderboard?top=100", Player("nobody"));
        using var history = await Send(_factory, HttpMethod.Get, "/api/players/nobody/matches", Player("nobody"));
        using var stats = await Send(_factory, HttpMethod.Get, "/api/players/nobody/stats", Player("nobody"));

        // Assert
        leaderboard.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(leaderboard)).GetArrayLength().Should().Be(0);
        history.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(history)).GetArrayLength().Should().Be(0);
        var totals = await Json(stats);
        (totals.GetProperty("gamesPlayed").GetInt32(), totals.GetProperty("wins").GetInt32(), totals.GetProperty("totalScore").GetInt32())
            .Should().Be((0, 0, 0));
    }

    [Fact]
    public async Task RecordedResult_IsVisibleThroughEveryReadRouteAfterAFreshRequest()
    {
        // Arrange
        var user = $"fresh-{Guid.NewGuid():N}";
        using var posted = await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result("m-fresh", user, 4, true, "2026-11-04T12:00:00+02:00"));

        // Act
        using var stats = await Send(_factory, HttpMethod.Get, $"/api/players/{user}/stats", Player(user));
        using var history = await Send(_factory, HttpMethod.Get, $"/api/players/{user}/matches", Player(user));
        using var leaderboard = await Send(_factory, HttpMethod.Get, "/api/leaderboard?top=100", Player(user));

        // Assert
        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(stats)).GetProperty("totalScore").GetInt32().Should().Be(4);
        var item = (await Json(history))[0];
        item.GetProperty("playedAt").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
        (await Json(leaderboard)).EnumerateArray().Should().Contain(row => row.GetProperty("userId").GetString() == user);
    }
}
