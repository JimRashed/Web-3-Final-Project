using FluentAssertions;
using QuizArena.Gameplay.Api.Contracts;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Exceptions;
using QuizArena.Gameplay.Domain.Repositories;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Application;

/// <summary>
/// Application rules with in-memory ports: no database, no HTTP.
/// </summary>
public sealed class GameplayApplicationTests
{
    private const string Host = "player-a";
    private const string Guest = "player-b";
    private const string LobbyId = "lobby-1";

    private static readonly AuthenticatedActor HostActor = new("issuer", Host, ActorKind.User);
    private static readonly AuthenticatedActor GuestActor = new("issuer", Guest, ActorKind.User);
    private static readonly AuthenticatedActor StrangerActor = new("issuer", "stranger", ActorKind.User);

    private static readonly Guid[] QuestionIds =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555")
    ];

    private readonly FakeMatches _matches = new();
    private readonly FakeLobby _lobby;
    private readonly FakeCatalog _catalog = new();

    public GameplayApplicationTests()
    {
        _lobby = new FakeLobby(new LobbyState(LobbyId, Host, [Host, Guest], IsLocked: true));
    }

    private GameplayApplication Sut() => new(_matches, _lobby, _catalog);

    [Fact]
    public async Task CreateAsync_WithLockedLobbyAndHost_CreatesFiveRoundMatch()
    {
        // Act
        var (match, created) = await Sut().CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Assert
        created.Should().BeTrue();
        match.Status.Should().Be("ACTIVE");
        match.PlayerAId.Should().Be(Host);
        match.PlayerBId.Should().Be(Guest);
        match.LobbyId.Should().Be(LobbyId);
        _matches.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_WhenLobbyAlreadyHasAMatch_ReturnsTheExistingOne()
    {
        // Arrange
        var sut = Sut();
        var (first, _) = await sut.CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Act
        var (second, created) = await sut.CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Assert
        created.Should().BeFalse();
        second.MatchId.Should().Be(first.MatchId);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownJoinCode_Returns404()
    {
        // Arrange
        var sut = new GameplayApplication(_matches, new FakeLobby(null), _catalog);

        // Act
        var act = () => sut.CreateAsync(HostActor, new CreateMatchRequest("ZZZ999"), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CreateAsync_WhenLobbyIsNotLocked_Returns409()
    {
        // Arrange — a fresh repository, so the lock check is reached (no match exists for the lobby yet).
        var sut = new GameplayApplication(new FakeMatches(), new FakeLobby(new LobbyState(LobbyId, Host, [Host, Guest], IsLocked: false)), _catalog);

        // Act
        var act = () => sut.CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreateAsync_WhenCallerIsNotTheHost_Returns403()
    {
        // Arrange — a fresh repository, so the host check is reached (no match exists for the lobby yet).
        var sut = new GameplayApplication(new FakeMatches(), _lobby, _catalog);

        // Act
        var act = () => sut.CreateAsync(GuestActor, new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetAsync_ByANonParticipant_Returns403()
    {
        // Arrange
        var (match, _) = await Sut().CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Act
        var act = () => Sut().GetAsync(StrangerActor, match.MatchId, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SubmitAnswerAsync_WithTheCorrectAnswer_ScoresAndPersists()
    {
        // Arrange
        var (match, _) = await Sut().CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Act
        var outcome = await Sut().SubmitAnswerAsync(
            HostActor,
            match.MatchId,
            new SubmitAnswerRequest(QuestionIds[0], "A"),
            CancellationToken.None);

        // Assert
        outcome.Correct.Should().BeTrue();
        outcome.PlayerAScore.Should().Be(1);
        outcome.RoundComplete.Should().BeFalse();
        _matches.Saved.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCurrentQuestionAsync_DoesNotRevealTheCorrectAnswer()
    {
        // Arrange
        var (match, _) = await Sut().CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Act
        var question = await Sut().GetCurrentQuestionAsync(HostActor, match.MatchId, CancellationToken.None);

        // Assert
        question.QuestionId.Should().Be(QuestionIds[0]);
        question.Options.Should().Equal("A", "B", "C", "D");
    }

    [Theory]
    [InlineData(Guest)]
    [InlineData("stranger")]
    public async Task CreateAsync_RepeatedByNonHost_Returns403InsteadOfTheExistingMatch(string subject)
    {
        // Arrange
        var sut = Sut();
        await sut.CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Act
        var act = () => sut.CreateAsync(new AuthenticatedActor("issuer", subject, ActorKind.User), new CreateMatchRequest("ABC123"), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SubmitAnswerAsync_WhenTheOtherAnswerWasSavedFirst_ReloadsAndRecordsBoth()
    {
        // Arrange: the guest's save collides once with the host's answer stored in between.
        var (match, _) = await Sut().CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);
        _matches.FailNextSaveWith = () => _matches.Update(match.MatchId, stored => stored.SubmitAnswer(Host, QuestionIds[0], "A", DateTime.UtcNow));

        // Act
        var outcome = await Sut().SubmitAnswerAsync(GuestActor, match.MatchId, new SubmitAnswerRequest(QuestionIds[0], "A"), CancellationToken.None);

        // Assert
        outcome.RoundComplete.Should().BeTrue();
        outcome.PlayerAScore.Should().Be(1);
        outcome.PlayerBScore.Should().Be(1);
    }

    [Fact]
    public async Task SubmitAnswerAsync_WhenEveryAttemptCollides_SurfacesTheConcurrencyException()
    {
        // Arrange
        var (match, _) = await Sut().CreateAsync(HostActor, new CreateMatchRequest("ABC123"), CancellationToken.None);
        _matches.AlwaysConflict = true;

        // Act
        var act = () => Sut().SubmitAnswerAsync(HostActor, match.MatchId, new SubmitAnswerRequest(QuestionIds[0], "A"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<MatchConcurrencyException>();
        _matches.ConflictCount.Should().Be(GameplayApplication.MaxAnswerAttempts);
    }

    private sealed class FakeMatches : IMatchRepository
    {
        private readonly Dictionary<string, Match> _byId = [];
        public List<Match> Saved { get; } = [];

        // Copies on read and on save, like a database: a failed attempt must not leak into the stored state.
        public Task<Match?> FindAsync(string matchId, CancellationToken cancellationToken) =>
            Task.FromResult(_byId.TryGetValue(matchId, out var stored) ? Copy(stored) : null);

        public void Update(string matchId, Action<Match> change) => change(_byId[matchId]);

        private static Match Copy(Match match) => Match.Rehydrate(
            match.MatchId, match.LobbyId, match.PlayerAId, match.PlayerBId, match.Status, match.PlayerAScore, match.PlayerBScore,
            match.CurrentQuestionIndex, match.WinnerUserId, match.Draw, match.CreatedAt, match.CompletedAt, match.Questions, match.Answers.ToArray());

        public Task<Match?> FindByLobbyAsync(string lobbyId, CancellationToken cancellationToken) =>
            Task.FromResult(_byId.Values.Where(match => match.LobbyId == lobbyId).Select(Copy).SingleOrDefault());

        /// <summary>Runs once before failing the next save, to simulate the other player's answer landing first.</summary>
        public Action? FailNextSaveWith { get; set; }
        public bool AlwaysConflict { get; set; }
        public int ConflictCount { get; private set; }

        public Task SaveAsync(Match match, CancellationToken cancellationToken)
        {
            if (AlwaysConflict || FailNextSaveWith is not null)
            {
                FailNextSaveWith?.Invoke();
                FailNextSaveWith = null;
                ConflictCount++;
                throw new MatchConcurrencyException(match.MatchId);
            }

            _byId[match.MatchId] = Copy(match);
            Saved.Add(match);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLobby(LobbyState? state) : ILobbyGateway
    {
        public Task<LobbyState?> GetAsync(string joinCode, CancellationToken cancellationToken) =>
            Task.FromResult(state);
    }

    private sealed class FakeCatalog : IQuestionCatalog
    {
        public Task<IReadOnlyList<QuestionSnapshot>> SnapshotAsync(int count, CancellationToken cancellationToken)
        {
            IReadOnlyList<QuestionSnapshot> snapshots = QuestionIds
                .Take(count)
                .Select(id => QuestionSnapshot.Create(id, $"Question {id}", ["A", "B", "C", "D"], "A"))
                .ToArray();
            return Task.FromResult(snapshots);
        }
    }
}
