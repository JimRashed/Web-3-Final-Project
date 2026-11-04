using QuizArena.Gameplay.Api.Contracts;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Enums;
using QuizArena.Gameplay.Domain.Exceptions;
using QuizArena.Gameplay.Domain.Repositories;

namespace QuizArena.Gameplay.Api.Services;

public sealed class GameplayApplication(
    IMatchRepository matches,
    ILobbyGateway lobbies,
    IQuestionCatalog questions) : IGameplayApplication
{
    /// <summary>Matches always run over exactly five rounds.</summary>
    public const int RoundsPerMatch = Match.RequiredQuestionCount;

    /// <summary>How often an answer is re-applied when the other player's answer was saved first.</summary>
    public const int MaxAnswerAttempts = 3;

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

        // A repeated start must be idempotent: the lobby already owns a match. Authorization is
        // rechecked, so only the host gets the existing match back.
        var existing = await matches.FindByLobbyAsync(lobby.LobbyId, cancellationToken);
        if (existing is not null)
        {
            return (ExistingForHost(actor, lobby, existing), false);
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

        try
        {
            await matches.SaveAsync(match, cancellationToken);
        }
        catch (MatchAlreadyStartedException)
        {
            // A concurrent start for the same lobby won the race: answer like a repeated start.
            var winner = await matches.FindByLobbyAsync(lobby.LobbyId, cancellationToken)
                ?? throw new InvalidOperationException("The lobby's match was reported but could not be loaded.");
            return (ExistingForHost(actor, lobby, winner), false);
        }

        return (MatchResponse.FromDomain(match), true);
    }

    private static MatchResponse ExistingForHost(AuthenticatedActor actor, LobbyState lobby, Match existing) =>
        string.Equals(lobby.HostSubject, actor.Subject, StringComparison.Ordinal)
            ? MatchResponse.FromDomain(existing)
            : throw new GameplayRequestException(StatusCodes.Status403Forbidden, "Only the lobby host can start the match.");

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
        for (var attempt = 1; ; attempt++)
        {
            var match = await LoadForParticipantAsync(actor, matchId, cancellationToken);
            var outcome = match.SubmitAnswer(actor.Subject, request.QuestionId, request.Answer, DateTime.UtcNow);
            try
            {
                await matches.SaveAsync(match, cancellationToken);
                return AnswerOutcomeResponse.FromDomain(outcome);
            }
            catch (MatchConcurrencyException) when (attempt < MaxAnswerAttempts)
            {
                // The other player's answer was saved first. Reload and re-apply ours on top; the
                // domain re-checks duplicates and round completion against the current state.
            }
        }
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
