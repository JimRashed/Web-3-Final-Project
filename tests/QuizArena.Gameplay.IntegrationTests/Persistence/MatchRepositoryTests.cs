using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Gameplay.Data.Context;
using QuizArena.Gameplay.Data.Repositories;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Enums;
using QuizArena.Gameplay.Domain.Exceptions;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Persistence;

/// <summary>
/// Relational tests against a real SQL Server database created (and dropped) per test.
/// Set QUIZARENA_TEST_SQLSERVER to use a server other than LocalDB.
/// </summary>
public sealed class MatchRepositoryTests : IAsyncLifetime
{
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private const string PlayerA = "player-a";
    private const string PlayerB = "player-b";
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Guid[] QuestionIds =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555")
    ];

    private readonly DbContextOptions<GameplayDbContext> _options;

    public MatchRepositoryTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaGameplay_Repo_{Guid.NewGuid():N}",
        };
        _options = new DbContextOptionsBuilder<GameplayDbContext>().UseSqlServer(connection.ConnectionString).Options;
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

    [Fact]
    public async Task SaveAsync_WithNewMatch_RoundTripsAggregate()
    {
        // Arrange
        var match = NewMatch();
        match.SubmitAnswer(PlayerA, QuestionIds[0], "A", Now);

        // Act
        await using (var context = NewContext())
        {
            await new MatchRepository(context).SaveAsync(match, CancellationToken.None);
        }

        // Assert
        await using var fresh = NewContext();
        var stored = await new MatchRepository(fresh).FindAsync(match.MatchId, CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.MatchId.Should().Be(match.MatchId);
        stored.LobbyId.Should().Be(match.LobbyId);
        stored.PlayerAScore.Should().Be(1);
        stored.CurrentQuestionIndex.Should().Be(0);
        stored.Questions.Should().HaveCount(5);
        stored.Questions[0].Options.Should().Equal("A", "B", "C", "D");
        stored.Answers.Should().ContainSingle().Which.UserId.Should().Be(PlayerA);
    }

    [Fact]
    public async Task SaveAsync_WithFinishedMatch_PreservesOutcomeAndCompletionTime()
    {
        // Arrange
        var match = NewMatch();
        Finish(match);

        // Act
        await using (var context = NewContext())
        {
            await new MatchRepository(context).SaveAsync(match, CancellationToken.None);
        }

        // Assert
        await using var fresh = NewContext();
        var stored = await new MatchRepository(fresh).FindAsync(match.MatchId, CancellationToken.None);

        stored!.Status.Should().Be(MatchStatus.Finished);
        stored.WinnerUserId.Should().Be(PlayerA);
        stored.Draw.Should().BeFalse();
        stored.CompletedAt.Should().Be(Now);
        stored.CurrentQuestion.Should().BeNull();
        stored.Answers.Should().HaveCount(10);
    }

    [Fact]
    public async Task SaveAsync_WhenMatchAlreadyStored_ReplacesState()
    {
        // Arrange
        var match = NewMatch();
        await using (var context = NewContext())
        {
            await new MatchRepository(context).SaveAsync(match, CancellationToken.None);
        }

        match.SubmitAnswer(PlayerA, QuestionIds[0], "A", Now);

        // Act
        await using (var context = NewContext())
        {
            await new MatchRepository(context).SaveAsync(match, CancellationToken.None);
        }

        // Assert
        await using var fresh = NewContext();
        var stored = await new MatchRepository(fresh).FindAsync(match.MatchId, CancellationToken.None);
        stored!.PlayerAScore.Should().Be(1);
        stored.Answers.Should().ContainSingle();
    }

    [Fact]
    public async Task FindAsync_WhenMatchUnknown_ReturnsNull()
    {
        // Arrange
        await using var context = NewContext();

        // Act
        var stored = await new MatchRepository(context).FindAsync("missing", CancellationToken.None);

        // Assert
        stored.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WhenAnotherRequestChangedTheMatch_ThrowsConcurrencyConflict()
    {
        // Arrange — the match exists, then two requests load it independently.
        var match = NewMatch();
        await using (var context = NewContext())
        {
            await new MatchRepository(context).SaveAsync(match, CancellationToken.None);
        }

        await using var firstRequest = NewContext();
        await using var secondRequest = NewContext();
        var firstRepository = new MatchRepository(firstRequest);
        var secondRepository = new MatchRepository(secondRequest);

        var firstView = await firstRepository.FindAsync(match.MatchId, CancellationToken.None);
        var secondView = await secondRepository.FindAsync(match.MatchId, CancellationToken.None);

        // Act — the first request records player A's answer and saves.
        firstView!.SubmitAnswer(PlayerA, QuestionIds[0], "A", Now);
        await firstRepository.SaveAsync(firstView, CancellationToken.None);

        // The second request, still holding the stale row version, records player B's answer.
        secondView!.SubmitAnswer(PlayerB, QuestionIds[0], "A", Now);
        var act = () => secondRepository.SaveAsync(secondView, CancellationToken.None);

        // Assert — refusing the stale write is what prevents the lost answer.
        await act.Should().ThrowAsync<MatchConcurrencyException>();
    }

    [Fact]
    public async Task SaveAsync_WhenTheMatchChangedAfterItWasRead_ThrowsInsteadOfLosingTheOtherAnswer()
    {
        // Arrange: both players read the match, then save one after the other (no overlap inside SaveAsync).
        await using (var setup = NewContext())
        {
            await new MatchRepository(setup).SaveAsync(NewMatch(), CancellationToken.None);
        }

        await using var contextA = NewContext();
        await using var contextB = NewContext();
        var repositoryA = new MatchRepository(contextA);
        var repositoryB = new MatchRepository(contextB);
        var matchA = (await repositoryA.FindAsync("match-1", CancellationToken.None))!;
        var matchB = (await repositoryB.FindAsync("match-1", CancellationToken.None))!;
        matchA.SubmitAnswer(PlayerA, QuestionIds[0], "A", Now);
        await repositoryA.SaveAsync(matchA, CancellationToken.None);
        matchB.SubmitAnswer(PlayerB, QuestionIds[0], "B", Now);

        // Act
        var act = () => repositoryB.SaveAsync(matchB, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<MatchConcurrencyException>();
        await using var fresh = NewContext();
        var stored = await new MatchRepository(fresh).FindAsync("match-1", CancellationToken.None);
        stored!.Answers.Should().ContainSingle(answer => answer.UserId == PlayerA);
    }

    private static Match NewMatch() =>
        Match.Create(
            "match-1",
            "lobby-1",
            PlayerA,
            PlayerB,
            QuestionIds.Select(id => QuestionSnapshot.Create(id, $"Question {id}", ["A", "B", "C", "D"], "A")).ToList(),
            Now);

    private static void Finish(Match match)
    {
        for (var round = 0; round < QuestionIds.Length; round++)
        {
            match.SubmitAnswer(PlayerA, QuestionIds[round], "A", Now);
            match.SubmitAnswer(PlayerB, QuestionIds[round], "B", Now);
        }
    }

    private GameplayDbContext NewContext() => new(_options);
}
