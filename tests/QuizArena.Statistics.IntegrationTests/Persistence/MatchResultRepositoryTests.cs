using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Statistics.Data.Context;
using QuizArena.Statistics.Data.Repositories;
using QuizArena.Statistics.Domain.Entities;
using QuizArena.Statistics.Domain.Enums;
using QuizArena.Statistics.Domain.Exceptions;
using QuizArena.Statistics.Domain.Services;
using Xunit;

namespace QuizArena.Statistics.IntegrationTests.Persistence;

/// <summary>
/// Relational tests against a real SQL Server database created (and dropped) per test.
/// SQL aggregation, ordering and paging are checked against the pure StatisticsCalculator on the same data.
/// Set QUIZARENA_TEST_SQLSERVER to use a server other than LocalDB.
/// </summary>
public sealed class MatchResultRepositoryTests : IAsyncLifetime
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private const string OtherIssuer = "http://localhost:8080/realms/other";
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private static readonly DateTime Start = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly DbContextOptions<StatisticsDbContext> _options;

    public MatchResultRepositoryTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaStatistics_Repo_{Guid.NewGuid():N}",
        };
        _options = new DbContextOptionsBuilder<StatisticsDbContext>().UseSqlServer(connection.ConnectionString).Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
    }

    private static MatchResult New(string userId, string matchId, int score, bool won, int minutes = 0, string issuer = Issuer) =>
        MatchResult.Record(issuer, matchId, userId, score, won, Start.AddMinutes(minutes));

    [Fact]
    public async Task RecordAsync_WithNewResult_PersistsAndReadsBackFromFreshContext()
    {
        // Act
        ResultReplayDecision decision;
        await using (var context = NewContext())
        {
            decision = await new MatchResultRepository(context).RecordAsync(New("player-a", "m1", 3, true), CancellationToken.None);
        }

        // Assert
        decision.Should().Be(ResultReplayDecision.New);
        await using var fresh = NewContext();
        var stored = await new MatchResultRepository(fresh).FindAsync(Issuer, "m1", "player-a", CancellationToken.None);
        stored!.Score.Should().Be(3);
        stored.Won.Should().BeTrue();
        stored.PlayedAt.Should().Be(Start);
        stored.PlayedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task RecordAsync_WithIdenticalReplay_ReturnsIdempotentAndKeepsOneRow()
    {
        // Arrange
        await Record(New("player-a", "m1", 3, true));

        // Act
        ResultReplayDecision decision;
        await using (var context = NewContext())
        {
            decision = await new MatchResultRepository(context).RecordAsync(New("player-a", "m1", 3, true), CancellationToken.None);
        }

        // Assert
        decision.Should().Be(ResultReplayDecision.IdempotentReplay);
        (await Count()).Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_WithConflictingReplay_ThrowsAndKeepsOriginal()
    {
        // Arrange
        await Record(New("player-a", "m1", 3, true));

        // Act
        await using var context = NewContext();
        var act = () => new MatchResultRepository(context).RecordAsync(New("player-a", "m1", 2, false), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ResultConflictException>();
        await using var fresh = NewContext();
        (await new MatchResultRepository(fresh).FindAsync(Issuer, "m1", "player-a", CancellationToken.None))!.Score.Should().Be(3);
    }

    [Fact]
    public async Task RecordAsync_WithConcurrentIdenticalDeliveries_StoresExactlyOneRow()
    {
        // Act: eight retries of the same delivery racing each other.
        var decisions = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = NewContext();
            return await new MatchResultRepository(context).RecordAsync(New("player-a", "m1", 4, true), CancellationToken.None);
        }));

        // Assert
        decisions.Count(d => d == ResultReplayDecision.New).Should().BeLessThanOrEqualTo(1);
        decisions.Should().OnlyContain(d => d == ResultReplayDecision.New || d == ResultReplayDecision.IdempotentReplay);
        (await Count()).Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_WithConcurrentConflictingDeliveries_StoresOneAndRejectsTheOther()
    {
        // Act
        var outcomes = await Task.WhenAll(new[] { New("player-a", "m1", 1, false), New("player-a", "m1", 5, true) }.Select(async result =>
        {
            await using var context = NewContext();
            try
            {
                await new MatchResultRepository(context).RecordAsync(result, CancellationToken.None);
                return "stored";
            }
            catch (ResultConflictException)
            {
                return "conflict";
            }
        }));

        // Assert
        outcomes.Should().BeEquivalentTo(["stored", "conflict"]);
        (await Count()).Should().Be(1);
    }

    [Fact]
    public async Task Keys_CompareCaseSensitivelyAndPerIssuer()
    {
        // Arrange + Act
        await Record(New("abc", "m1", 1, false));
        await Record(New("ABC", "m1", 2, false));
        await Record(New("abc", "M1", 3, false));
        await Record(New("abc", "m1", 4, false, issuer: OtherIssuer));

        // Assert
        (await Count()).Should().Be(4);
        await using var context = NewContext();
        var repository = new MatchResultRepository(context);
        (await repository.FindAsync(Issuer, "m1", "ABC", CancellationToken.None))!.Score.Should().Be(2);
        (await repository.FindAsync(Issuer, "m1", "Abc", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task GetTotalsAsync_MatchesCalculatorIncludingDrawsAndIssuerFilter()
    {
        // Arrange
        var results = new[]
        {
            New("player-a", "m1", 4, true),
            New("player-a", "m2", 1, false),
            New("player-a", "m3", 3, false), // draw
            New("player-b", "m1", 2, false),
        };
        foreach (var result in results)
        {
            await Record(result);
        }

        await Record(New("player-a", "m9", 5, true, issuer: OtherIssuer));

        // Act
        await using var context = NewContext();
        var repository = new MatchResultRepository(context);
        var totals = await repository.GetTotalsAsync(Issuer, "player-a", CancellationToken.None);
        var unknown = await repository.GetTotalsAsync(Issuer, "nobody", CancellationToken.None);

        // Assert
        totals.Should().Be(StatisticsCalculator.Summarize("player-a", results));
        totals.Should().Be(new PlayerTotals("player-a", 3, 1, 8));
        unknown.Should().Be(new PlayerTotals("nobody", 0, 0, 0));
    }

    [Fact]
    public async Task GetHistoryAsync_PagesNewestFirstWithOrdinalMatchIdTieBreak()
    {
        // Arrange
        var results = new[]
        {
            New("player-a", "m-old", 1, false, minutes: 0),
            New("player-a", "m-b", 2, false, minutes: 10),
            New("player-a", "m-a", 3, true, minutes: 10),
            New("player-a", "M-c", 4, true, minutes: 10),
            New("player-a", "m-new", 5, true, minutes: 20),
        };
        foreach (var result in results)
        {
            await Record(result);
        }

        await Record(New("player-b", "m-x", 1, false, minutes: 30));

        // Act
        await using var context = NewContext();
        var repository = new MatchResultRepository(context);
        var page1 = await repository.GetHistoryAsync(Issuer, "player-a", 1, 2, CancellationToken.None);
        var page2 = await repository.GetHistoryAsync(Issuer, "player-a", 2, 2, CancellationToken.None);
        var page3 = await repository.GetHistoryAsync(Issuer, "player-a", 3, 2, CancellationToken.None);
        var beyond = await repository.GetHistoryAsync(Issuer, "player-a", 4, 2, CancellationToken.None);

        // Assert
        var expected = StatisticsCalculator.OrderHistory(results).Select(r => r.MatchId).ToList();
        expected.Should().Equal("m-new", "M-c", "m-a", "m-b", "m-old");
        page1.Concat(page2).Concat(page3).Select(r => r.MatchId).Should().Equal(expected);
        beyond.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLeaderboardAsync_MatchesCalculatorOrderingAndTop()
    {
        // Arrange
        var results = new[]
        {
            New("carol", "m1", 5, true),
            New("alice", "m2", 3, false),
            New("alice", "m3", 2, true),
            New("bob", "m4", 5, false),
            New("Zed", "m5", 5, true),
            New("dave", "m6", 1, false),
        };
        foreach (var result in results)
        {
            await Record(result);
        }

        await Record(New("outsider", "m7", 5, true, issuer: OtherIssuer));

        // Act
        await using var context = NewContext();
        var repository = new MatchResultRepository(context);
        var board = await repository.GetLeaderboardAsync(Issuer, 10, CancellationToken.None);
        var top2 = await repository.GetLeaderboardAsync(Issuer, 2, CancellationToken.None);

        // Assert
        board.Should().Equal(StatisticsCalculator.BuildLeaderboard(results, 10));
        board.Select(e => e.UserId).Should().Equal("Zed", "alice", "carol", "bob", "dave");
        top2.Select(e => e.UserId).Should().Equal("Zed", "alice");
    }

    [Fact]
    public async Task Queries_WithOutOfRangeArguments_ThrowBeforeQuerying()
    {
        // Arrange
        await using var context = NewContext();
        var repository = new MatchResultRepository(context);

        // Act + Assert
        await FluentActions.Awaiting(() => repository.GetHistoryAsync(Issuer, "player-a", 0, 20, CancellationToken.None))
            .Should().ThrowAsync<InvalidMatchResultException>();
        await FluentActions.Awaiting(() => repository.GetHistoryAsync(Issuer, "player-a", 1, 101, CancellationToken.None))
            .Should().ThrowAsync<InvalidMatchResultException>();
        await FluentActions.Awaiting(() => repository.GetLeaderboardAsync(Issuer, 0, CancellationToken.None))
            .Should().ThrowAsync<InvalidMatchResultException>();
    }

    [Fact]
    public async Task HistoricalScoreAboveFive_IsStoredButNegativeScoreIsRejectedByDatabase()
    {
        // Arrange
        await using var context = NewContext();
        context.MatchResults.Add(MatchResult.Rehydrate(Issuer, "legacy-1", "player-a", 12, true, Start));
        await context.SaveChangesAsync();

        // Act
        var act = () => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO MatchResults (Issuer, MatchId, UserId, Score, Won, PlayedAt) VALUES ({0}, 'legacy-2', 'player-a', -1, 0, SYSUTCDATETIME())",
            Issuer);

        // Assert
        (await new MatchResultRepository(context).FindAsync(Issuer, "legacy-1", "player-a", CancellationToken.None))!.Score.Should().Be(12);
        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("CK_MatchResults_Score_NonNegative");
    }

    [Fact]
    public async Task Reads_DoNotTrackEntities()
    {
        // Arrange
        await Record(New("player-a", "m1", 3, true));

        // Act
        await using var context = NewContext();
        var repository = new MatchResultRepository(context);
        await repository.FindAsync(Issuer, "m1", "player-a", CancellationToken.None);
        await repository.GetHistoryAsync(Issuer, "player-a", 1, 20, CancellationToken.None);

        // Assert
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    private async Task Record(MatchResult result)
    {
        await using var context = NewContext();
        await new MatchResultRepository(context).RecordAsync(result, CancellationToken.None);
    }

    private async Task<int> Count()
    {
        await using var context = NewContext();
        return await context.MatchResults.CountAsync();
    }

    private StatisticsDbContext NewContext() => new(_options);
}
