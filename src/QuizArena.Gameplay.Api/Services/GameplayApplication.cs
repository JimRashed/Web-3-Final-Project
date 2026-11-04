using QuizArena.Gameplay.Api.Contracts;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Enums;
using QuizArena.Gameplay.Domain.Repositories;

namespace QuizArena.Gameplay.Api.Services;

public sealed class GameplayApplication(
    IMatchRepository matches,
    ILobbyGateway lobbies,
    IQuestionCatalog questions) : IGameplayApplication
{
    /// <summary>Matches always run over exactly five rounds.</summary>
    public const int RoundsPerMatch = Match.RequiredQuestionCount;

    public async Task<(MatchResponse Match, bool Created)> CreateAsync(
        AuthenticatedActor actor,
        CreateMatchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JoinCode))
        {
            throw new GameplayRequestException(StatusCodes.Status400BadRequest, "A join code is required.");
        }

        var lobby = await lobbies.GetAsync(request.JoinCode, cancellationToken)
            ?? throw new GameplayRequestException(StatusCodes.Status404NotFound, "The join code is unknown.");

        // A repeated start must be idempotent: the lobby already owns a match.
        var existing = await matches.FindByLobbyAsync(lobby.LobbyId, cancellationToken);
        if (existing is not null)
        {
            return (MatchResponse.FromDomain(existing), false);
        }

        if (!lobby.IsLocked)
        {
            throw new GameplayRequestException(StatusCodes.Status409Conflict, "The lobby must be locked before a match can start.");
        }

        if (!string.Equals(lobby.HostSubject, actor.Subject, StringComparison.Ordinal))
        {
            throw new GameplayRequestException(StatusCodes.Status403Forbidden, "Only the lobby host can start the match.");
        }

        if (lobby.Members.Count != 2 || !lobby.Members.Contains(actor.Subject, StringComparer.Ordinal))
        {
            throw new GameplayRequestException(StatusCodes.Status409Conflict, "A locked lobby needs exactly two members.");
        }

        var opponent = lobby.Members.First(member => !string.Equals(member, actor.Subject, StringComparison.Ordinal));
        var snapshots = await questions.SnapshotAsync(RoundsPerMatch, cancellationToken);

        var match = Match.Create(
            Guid.NewGuid().ToString(),
            lobby.LobbyId,
            actor.Subject,
            opponent,
            snapshots,
            DateTime.UtcNow);

        await matches.SaveAsync(match, cancellationToken);
        return (MatchResponse.FromDomain(match), true);
    }

    public async Task<MatchResponse> GetAsync(AuthenticatedActor actor, string matchId, CancellationToken cancellationToken) =>
        MatchResponse.FromDomain(await LoadForParticipantAsync(actor, matchId, cancellationToken));

    public async Task<CurrentQuestionResponse> GetCurrentQuestionAsync(
        AuthenticatedActor actor,
        string matchId,
        CancellationToken cancellationToken)
    {
        var match = await LoadForParticipantAsync(actor, matchId, cancellationToken);

        // Reading never advances the match; a finished match has no current question.
        return match.CurrentQuestion is null
            ? throw new GameplayRequestException(StatusCodes.Status409Conflict, "The match is finished.")
            : CurrentQuestionResponse.FromDomain(match.CurrentQuestion);
    }

    public async Task<AnswerOutcomeResponse> SubmitAnswerAsync(
        AuthenticatedActor actor,
        string matchId,
        SubmitAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var match = await LoadForParticipantAsync(actor, matchId, cancellationToken);

        var outcome = match.SubmitAnswer(actor.Subject, request.QuestionId, request.Answer, DateTime.UtcNow);
        await matches.SaveAsync(match, cancellationToken);

        return AnswerOutcomeResponse.FromDomain(outcome);
    }

    private async Task<Match> LoadForParticipantAsync(AuthenticatedActor actor, string matchId, CancellationToken cancellationToken)
    {
        var match = await matches.FindAsync(matchId, cancellationToken)
            ?? throw new GameplayRequestException(StatusCodes.Status404NotFound, "The match is unknown.");

        if (!match.IsMember(actor.Subject))
        {
            throw new GameplayRequestException(StatusCodes.Status403Forbidden, "Only a participant can see this match.");
        }

        return match;
    }
}
