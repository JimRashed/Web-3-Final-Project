using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static QuizArena.Statistics.IntegrationTests.Endpoints.StatisticsHttp;

namespace QuizArena.Statistics.IntegrationTests.Endpoints;

/// <summary>HTTP tests for result ingestion, stats and history, through the real pipeline and database.</summary>
public sealed class StatisticsEndpointsTests : IClassFixture<StatisticsApiFactory>
{
    private readonly StatisticsApiFactory _factory;

    public StatisticsEndpointsTests(StatisticsApiFactory factory)
    {
        _factory = factory;
    }

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task PostResult_FromGameplay_Returns200EmptyAndPersists()
    {
        // Arrange
        var user = NewId("player");
        var match = NewId("match");

        // Act
        using var response = await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result(match, user, 3, true, "2026-11-04T12:00:00Z"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        await using var db = _factory.NewDbContext();
        (await db.MatchResults.CountAsync(r => r.UserId == user && r.MatchId == match)).Should().Be(1);
    }

    [Fact]
    public async Task PostResult_IdenticalRetry_Returns200WithoutDuplicate()
    {
        // Arrange
        var user = NewId("player");
        var body = Result(NewId("match"), user, 2, false, "2026-11-04T12:00:00Z");
        using var first = await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), body);

        // Act
        using var retry = await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), body);

        // Assert
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = _factory.NewDbContext();
        (await db.MatchResults.CountAsync(r => r.UserId == user)).Should().Be(1);
    }

    [Fact]
    public async Task PostResult_ConflictingRetry_Returns409Problem()
    {
        // Arrange
        var user = NewId("player");
        var match = NewId("match");
        using var first = await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result(match, user, 2, false, "2026-11-04T12:00:00Z"));

        // Act
        using var conflict = await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result(match, user, 5, true, "2026-11-04T12:00:00Z"));

        // Assert
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        conflict.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Theory]
    [InlineData("""{ "userId": "p", "score": 1, "won": false, "playedAt": "2026-11-04T12:00:00Z" }""", "matchId")]
    [InlineData("""{ "matchId": "m", "userId": "p", "won": false, "playedAt": "2026-11-04T12:00:00Z" }""", "score")]
    [InlineData("""{ "matchId": "m", "userId": "p", "score": 6, "won": true, "playedAt": "2026-11-04T12:00:00Z" }""", "score")]
    [InlineData("""{ "matchId": "m", "userId": "p", "score": 1, "won": false, "playedAt": "2026-11-04T12:00:00" }""", "playedAt")]
    public async Task PostResult_WithInvalidBody_Returns400WithFieldError(string body, string field)
    {
        // Act
        using var response = await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await Json(response)).GetProperty("errors");
        errors.EnumerateObject().Select(e => e.Name).Should().Contain(name => name.Contains(field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostResult_WithUnknownField_Returns400AndWritesNothing()
    {
        // Arrange
        var user = NewId("player");
        var body = $$"""{ "matchId": "m1", "userId": "{{user}}", "score": 1, "won": false, "playedAt": "2026-11-04T12:00:00Z", "issuer": "evil" }""";

        // Act
        using var response = await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var db = _factory.NewDbContext();
        (await db.MatchResults.AnyAsync(r => r.UserId == user)).Should().BeFalse();
    }

    [Fact]
    public async Task PostResult_WithPlayerTokenOrNoToken_IsRejected()
    {
        // Arrange
        var body = Result(NewId("match"), NewId("player"), 1, false, "2026-11-04T12:00:00Z");

        // Act
        using var player = await Send(_factory, HttpMethod.Post, "/api/results", Player("player-a", "results.write"), body);
        using var anonymous = await Send(_factory, HttpMethod.Post, "/api/results", null, body);

        // Assert
        player.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStats_ForSelf_ReturnsTotalsIncludingDraw()
    {
        // Arrange
        var user = NewId("player");
        await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result(NewId("m"), user, 4, true, "2026-11-04T12:00:00Z"));
        await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result(NewId("m"), user, 3, false, "2026-11-04T12:05:00Z"));

        // Act
        using var response = await Send(_factory, HttpMethod.Get, $"/api/players/{user}/stats", Player(user));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await Json(response);
        stats.GetProperty("userId").GetString().Should().Be(user);
        stats.GetProperty("gamesPlayed").GetInt32().Should().Be(2);
        stats.GetProperty("wins").GetInt32().Should().Be(1);
        stats.GetProperty("totalScore").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task GetStats_ForNewPlayer_ReturnsZeros()
    {
        // Arrange
        var user = NewId("player");

        // Act
        using var response = await Send(_factory, HttpMethod.Get, $"/api/players/{user}/stats", Player(user));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(response)).GetProperty("gamesPlayed").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("stats")]
    [InlineData("matches")]
    public async Task SelfOnlyRoutes_ForAnotherPlayer_Return403Problem(string route)
    {
        // Act
        using var response = await Send(_factory, HttpMethod.Get, $"/api/players/player-b/{route}", Player("player-a"));
        using var caseVariant = await Send(_factory, HttpMethod.Get, $"/api/players/Player-A/{route}", Player("player-a"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        caseVariant.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetHistory_PagesNewestFirst()
    {
        // Arrange
        var user = NewId("player");
        await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result("m-1", user, 1, false, "2026-11-04T12:00:00Z"));
        await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result("m-2", user, 2, false, "2026-11-04T13:00:00Z"));
        await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result("m-3", user, 3, true, "2026-11-04T14:00:00Z"));

        // Act
        using var page1 = await Send(_factory, HttpMethod.Get, $"/api/players/{user}/matches?page=1&pageSize=2", Player(user));
        using var page2 = await Send(_factory, HttpMethod.Get, $"/api/players/{user}/matches?page=2&pageSize=2", Player(user));
        using var beyond = await Send(_factory, HttpMethod.Get, $"/api/players/{user}/matches?page=3&pageSize=2", Player(user));

        // Assert
        (await Json(page1)).EnumerateArray().Select(i => i.GetProperty("matchId").GetString()).Should().Equal("m-3", "m-2");
        (await Json(page2)).EnumerateArray().Select(i => i.GetProperty("matchId").GetString()).Should().Equal("m-1");
        (await Json(beyond)).GetArrayLength().Should().Be(0);
        (await Json(page1))[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("matchId", "score", "won", "playedAt");
    }

    [Theory]
    [InlineData("page=0&pageSize=20")]
    [InlineData("page=1&pageSize=0")]
    [InlineData("page=1&pageSize=101")]
    [InlineData("page=abc")]
    public async Task GetHistory_WithInvalidPaging_Returns400(string query)
    {
        // Act
        using var response = await Send(_factory, HttpMethod.Get, $"/api/players/player-a/matches?{query}", Player("player-a"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistory_InvalidPagingForAnotherPlayer_Returns400Before403()
    {
        // Act
        using var response = await Send(_factory, HttpMethod.Get, "/api/players/player-b/matches?page=0", Player("player-a"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReadRoutes_WithoutRequiredScope_Return403()
    {
        // Act
        using var stats = await Send(_factory, HttpMethod.Get, "/api/players/player-a/stats", Player("player-a", "leaderboard.read"));
        using var machine = await Send(_factory, HttpMethod.Get, "/api/players/player-a/stats", Gameplay("stats.read"));

        // Assert
        stats.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        machine.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OpenApiDocument_InDevelopment_ListsExactlyTheContractOperations()
    {
        // Act
        using var response = await Send(_factory, HttpMethod.Get, "/openapi/v1.json", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Operations(await Json(response)).Should().BeEquivalentTo(Operations(ReadContract()));
    }
}
