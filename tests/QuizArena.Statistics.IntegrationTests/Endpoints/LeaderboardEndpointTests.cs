using System.Net;
using FluentAssertions;
using Xunit;
using static QuizArena.Statistics.IntegrationTests.Endpoints.StatisticsHttp;

namespace QuizArena.Statistics.IntegrationTests.Endpoints;

/// <summary>
/// Leaderboard HTTP tests. A separate class gets its own database (xUnit fixture per class), so other tests'
/// results cannot change the ranking.
/// </summary>
public sealed class LeaderboardEndpointTests : IClassFixture<StatisticsApiFactory>
{
    private readonly StatisticsApiFactory _factory;

    public LeaderboardEndpointTests(StatisticsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLeaderboard_RanksByScoreThenWinsThenUserIdAndHonoursTop()
    {
        // Arrange: Zed and alice tie on score and wins; ordinal order puts "Zed" first.
        foreach (var (match, user, score, won) in new[]
        {
            ("m1", "carol", 5, true), ("m2", "alice", 3, false), ("m3", "alice", 2, true),
            ("m4", "bob", 5, false), ("m5", "Zed", 5, true), ("m6", "dave", 1, false),
        })
        {
            using var posted = await Send(_factory, HttpMethod.Post, "/api/results", Gameplay(), Result(match, user, score, won, "2026-11-04T12:00:00Z"));
            posted.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Act
        using var all = await Send(_factory, HttpMethod.Get, "/api/leaderboard", Player("anyone"));
        using var top2 = await Send(_factory, HttpMethod.Get, "/api/leaderboard?top=2", Player("anyone"));

        // Assert
        all.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = (await Json(all)).EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("userId").GetString()).Should().Equal("Zed", "alice", "carol", "bob", "dave");
        rows[1].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("userId", "totalScore", "wins");
        (await Json(top2)).EnumerateArray().Select(r => r.GetProperty("userId").GetString()).Should().Equal("Zed", "alice");
    }

    [Theory]
    [InlineData("top=0")]
    [InlineData("top=101")]
    [InlineData("top=ten")]
    public async Task GetLeaderboard_WithInvalidTop_Returns400(string query)
    {
        // Act
        using var response = await Send(_factory, HttpMethod.Get, $"/api/leaderboard?{query}", Player("anyone"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetLeaderboard_WithoutLeaderboardScopeOrAsMachine_Returns403()
    {
        // Act
        using var noScope = await Send(_factory, HttpMethod.Get, "/api/leaderboard", Player("anyone", "stats.read"));
        using var machine = await Send(_factory, HttpMethod.Get, "/api/leaderboard", Gameplay("leaderboard.read"));

        // Assert
        noScope.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        machine.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
