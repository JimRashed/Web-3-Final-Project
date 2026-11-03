using FluentAssertions;
using QuizArena.Statistics.Domain.Entities;
using QuizArena.Statistics.Domain.Enums;
using QuizArena.Statistics.Domain.Exceptions;
using QuizArena.Statistics.Domain.Services;
using Xunit;

namespace QuizArena.Statistics.UnitTests.Domain;

public sealed class StatisticsCalculatorTests
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private static readonly DateTime Start = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static MatchResult Result(string userId, string matchId, int score, bool won, int minutes = 0) =>
        MatchResult.Rehydrate(Issuer, matchId, userId, score, won, Start.AddMinutes(minutes));

    [Fact]
    public void Summarize_WithNoResults_ReturnsZeroTotals()
    {
        // Act
        var totals = StatisticsCalculator.Summarize("player-a", []);

        // Assert
        totals.Should().Be(new PlayerTotals("player-a", 0, 0, 0));
    }

    [Fact]
    public void Summarize_WithWinLossAndDraw_CountsDrawAsGameButNotWin()
    {
        // Arrange
        var results = new[]
        {
            Result("player-a", "m1", 4, won: true),
            Result("player-a", "m2", 1, won: false),
            Result("player-a", "m3", 3, won: false), // draw
        };

        // Act
        var totals = StatisticsCalculator.Summarize("player-a", results);

        // Assert
        totals.Should().Be(new PlayerTotals("player-a", GamesPlayed: 3, Wins: 1, TotalScore: 8));
    }

    [Fact]
    public void Summarize_IgnoresOtherPlayersAndCaseVariants()
    {
        // Arrange
        var results = new[]
        {
            Result("player-a", "m1", 2, won: true),
            Result("player-b", "m1", 1, won: false),
            Result("Player-A", "m2", 5, won: true),
        };

        // Act
        var totals = StatisticsCalculator.Summarize("player-a", results);

        // Assert
        totals.Should().Be(new PlayerTotals("player-a", 1, 1, 2));
    }

    [Fact]
    public void BuildLeaderboard_OrdersByScoreThenWinsThenUserIdOrdinal()
    {
        // Arrange
        var results = new[]
        {
            Result("carol", "m1", 5, won: true),
            Result("alice", "m2", 3, won: false),
            Result("alice", "m3", 2, won: true),   // alice: 5 points, 1 win
            Result("bob", "m4", 5, won: false),    // bob:   5 points, 0 wins
            Result("Zed", "m5", 5, won: true),     // Zed:   5 points, 1 win (uppercase sorts before lowercase)
            Result("dave", "m6", 1, won: false),
        };

        // Act
        var board = StatisticsCalculator.BuildLeaderboard(results, 10);

        // Assert
        board.Select(e => e.UserId).Should().Equal("Zed", "alice", "carol", "bob", "dave");
        board.Select(e => e.Rank).Should().Equal(1, 2, 3, 4, 5);
        board[1].Should().Be(new LeaderboardEntry(2, "alice", 5, 1, 2));
    }

    [Fact]
    public void BuildLeaderboard_IsIndependentOfInputOrder()
    {
        // Arrange
        var results = new[]
        {
            Result("b", "m1", 3, won: true),
            Result("a", "m2", 3, won: true),
            Result("c", "m3", 3, won: true),
        };

        // Act
        var forward = StatisticsCalculator.BuildLeaderboard(results);
        var reversed = StatisticsCalculator.BuildLeaderboard(results.Reverse());

        // Assert
        forward.Should().Equal(reversed);
        forward.Select(e => e.UserId).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void BuildLeaderboard_WithTopSmallerThanPlayers_ReturnsOnlyTop()
    {
        // Arrange
        var results = Enumerable.Range(1, 5).Select(i => Result($"p{i}", $"m{i}", i, won: false));

        // Act
        var board = StatisticsCalculator.BuildLeaderboard(results, 2);

        // Assert
        board.Select(e => e.UserId).Should().Equal("p5", "p4");
    }

    [Fact]
    public void BuildLeaderboard_WithNoResults_ReturnsEmpty()
    {
        // Act
        var board = StatisticsCalculator.BuildLeaderboard([]);

        // Assert
        board.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(StatisticsCalculator.MaxLeaderboardSize + 1)]
    public void BuildLeaderboard_WithTopOutOfRange_ThrowsInvalidMatchResult(int top)
    {
        // Act
        var act = () => StatisticsCalculator.BuildLeaderboard([], top);

        // Assert
        act.Should().Throw<InvalidMatchResultException>().Which.Field.Should().Be("top");
    }

    [Fact]
    public void BuildLeaderboard_WithTopAtMax_Succeeds()
    {
        // Act
        var act = () => StatisticsCalculator.BuildLeaderboard([], StatisticsCalculator.MaxLeaderboardSize);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void OrderHistory_SortsNewestFirstThenMatchIdOrdinal()
    {
        // Arrange
        var results = new[]
        {
            Result("player-a", "m-old", 1, won: false, minutes: 0),
            Result("player-a", "m-b", 2, won: false, minutes: 10),
            Result("player-a", "m-a", 3, won: true, minutes: 10),
            Result("player-a", "M-c", 4, won: true, minutes: 10),
        };

        // Act
        var history = StatisticsCalculator.OrderHistory(results);

        // Assert
        history.Select(r => r.MatchId).Should().Equal("M-c", "m-a", "m-b", "m-old");
    }

    [Fact]
    public void DecideReplay_WithNoStoredResult_ReturnsNew()
    {
        // Act
        var decision = StatisticsCalculator.DecideReplay(null, Result("player-a", "m1", 3, true));

        // Assert
        decision.Should().Be(ResultReplayDecision.New);
    }

    [Fact]
    public void DecideReplay_WithIdenticalStoredResult_ReturnsIdempotentReplay()
    {
        // Act
        var decision = StatisticsCalculator.DecideReplay(Result("player-a", "m1", 3, true), Result("player-a", "m1", 3, true));

        // Assert
        decision.Should().Be(ResultReplayDecision.IdempotentReplay);
    }

    [Fact]
    public void DecideReplay_WithDifferentStoredResult_ReturnsConflict()
    {
        // Act
        var decision = StatisticsCalculator.DecideReplay(Result("player-a", "m1", 3, true), Result("player-a", "m1", 2, true));

        // Assert
        decision.Should().Be(ResultReplayDecision.Conflict);
    }

    [Fact]
    public void DecideReplay_WithDifferentKey_ThrowsArgumentException()
    {
        // Act
        var act = () => StatisticsCalculator.DecideReplay(Result("player-a", "m1", 3, true), Result("player-b", "m1", 3, true));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ShouldPersist_MapsDecisionsAndThrowsOnConflict()
    {
        // Arrange
        var stored = Result("player-a", "m1", 3, true);

        // Act
        var persistNew = StatisticsCalculator.ShouldPersist(null, stored);
        var persistReplay = StatisticsCalculator.ShouldPersist(stored, Result("player-a", "m1", 3, true));
        var conflict = () => StatisticsCalculator.ShouldPersist(stored, Result("player-a", "m1", 3, false));

        // Assert
        persistNew.Should().BeTrue();
        persistReplay.Should().BeFalse();
        conflict.Should().Throw<ResultConflictException>().Which.UserId.Should().Be("player-a");
    }
}
