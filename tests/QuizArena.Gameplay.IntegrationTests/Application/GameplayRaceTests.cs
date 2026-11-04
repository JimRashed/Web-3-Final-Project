using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Gameplay.Api.Contracts;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Data.Context;
using QuizArena.Gameplay.Data.Repositories;
using QuizArena.Gameplay.Domain.Entities;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Application;

/// <summary>
/// Simultaneous requests through the application service and the real repository on SQL Server. Each request
/// gets its own DbContext, as it would per HTTP request. The assertions hold for every interleaving.
/// </summary>
public sealed class GameplayRaceTests : IAsyncLifetime
{
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private static readonly AuthenticatedActor Host = new("issuer", "player-a", ActorKind.User);
    private static readonly AuthenticatedActor Guest = new("issuer", "player-b", ActorKind.User);
    private readonly DbContextOptions<GameplayDbContext> _options;
    private readonly List<GameplayDbContext> _contexts = [];

    public GameplayRaceTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaGameplay_Race_{Guid.NewGuid():N}",
        };
        _options = new DbContextOptionsBuilder<GameplayDbContext>().UseSqlServer(connection.ConnectionString).Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new GameplayDbContext(_options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var context in _contexts)
        {
            await context.DisposeAsync();
        }

        await using var cleanup = new GameplayDbContext(_options);
        await cleanup.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task CreateAsync_TwoSimultaneousStarts_BothSucceedWithOneMatch()
    {
        for (var round = 0; round < 5; round++)
        {
            // Arrange
            var lobby = new Lobby($"lobby-{round}");

            // Act
            var results = await Task.WhenAll(
                Request(lobby).CreateAsync(Host, new CreateMatchRequest("ABC123"), CancellationToken.None),
                Request(lobby).CreateAsync(Host, new CreateMatchRequest("ABC123"), CancellationToken.None));

            // Assert
            results.Count(result => result.Created).Should().Be(1);
            results.Select(result => result.Match.MatchId).Distinct().Should().ContainSingle();
            await using var check = new GameplayDbContext(_options);
            (await check.Matches.CountAsync(match => match.LobbyId == lobby.Id)).Should().Be(1);
        }
    }

    [Fact]
    public async Task SubmitAnswerAsync_BothPlayersAtOnce_RecordsBothAnswers()
    {
        // Arrange
        var lobby = new Lobby("lobby-answers");
        var (match, _) = await Request(lobby).CreateAsync(Host, new CreateMatchRequest("ABC123"), CancellationToken.None);

        for (var round = 0; round < Match.RequiredQuestionCount; round++)
        {
            var question = await Request(lobby).GetCurrentQuestionAsync(Host, match.MatchId, CancellationToken.None);

            // Act
            var outcomes = await Task.WhenAll(
                Request(lobby).SubmitAnswerAsync(Host, match.MatchId, new SubmitAnswerRequest(question.QuestionId, "A"), CancellationToken.None),
                Request(lobby).SubmitAnswerAsync(Guest, match.MatchId, new SubmitAnswerRequest(question.QuestionId, "B"), CancellationToken.None));

            // Assert: exactly one of the two saw the round complete, and both answers counted.
            outcomes.Count(outcome => outcome.RoundComplete).Should().Be(1);
        }

        var final = await Request(lobby).GetAsync(Host, match.MatchId, CancellationToken.None);
        final.Status.Should().Be("FINISHED");
        final.PlayerAScore.Should().Be(Match.RequiredQuestionCount);
        final.PlayerBScore.Should().Be(0);
        final.WinnerUserId.Should().Be(Host.Subject);
    }

    private GameplayApplication Request(Lobby lobby)
    {
        var context = new GameplayDbContext(_options);
        _contexts.Add(context);
        return new GameplayApplication(new MatchRepository(context), lobby, new Catalog());
    }

    private sealed class Lobby(string id) : ILobbyGateway
    {
        public string Id { get; } = id;

        public Task<LobbyState?> GetAsync(string joinCode, CancellationToken cancellationToken) =>
            Task.FromResult<LobbyState?>(new LobbyState(Id, Host.Subject, [Host.Subject, Guest.Subject], IsLocked: true));
    }

    private sealed class Catalog : IQuestionCatalog
    {
        public Task<IReadOnlyList<QuestionSnapshot>> SnapshotAsync(int count, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<QuestionSnapshot>>(Enumerable.Range(1, count)
                .Select(index => QuestionSnapshot.Create(Guid.NewGuid(), $"Question {index}", ["A", "B", "C", "D"], "A"))
                .ToArray());
    }
}
