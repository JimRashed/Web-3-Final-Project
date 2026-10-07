using FluentAssertions;
using Moq;
using QuizArena.Api.Services;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Statistics.Entities;
using QuizArena.Statistics.Api.Services;

namespace QuizArena.UnitTests.Services;

public sealed class StatisticsServiceTests
{
    [Fact]
    public async Task GetStatsAsync_calculates_aggregate_values()
    {
        var user = Guid.NewGuid();
        var repository = new Mock<IStatisticsRepository>();
        repository.Setup(x => x.GetByUserAsync(user)).ReturnsAsync(new List<MatchResult> { new() { UserId = user, Score = 5, Won = true }, new() { UserId = user, Score = 2, Won = false } });

        var result = await new StatisticsService(repository.Object).GetStatsAsync(user);

        result.Should().BeEquivalentTo(new { UserId = user, GamesPlayed = 2, Wins = 1, TotalScore = 7 });
    }

    [Fact]
    public async Task GetLeaderboardAsync_groups_orders_and_limits_results()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var repository = new Mock<IStatisticsRepository>();
        repository.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<MatchResult> { new() { UserId = first, Score = 3, Won = true }, new() { UserId = first, Score = 2 }, new() { UserId = second, Score = 4 } });

        var result = await new StatisticsService(repository.Object).GetLeaderboardAsync(1);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new { UserId = first, TotalScore = 5, Wins = 1 });
    }
}
