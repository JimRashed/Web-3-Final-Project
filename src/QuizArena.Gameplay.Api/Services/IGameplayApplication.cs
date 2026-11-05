using QuizArena.Gameplay.Api.Contracts;
using QuizArena.Gameplay.Api.Security;

namespace QuizArena.Gameplay.Api.Services;

/// <summary>
/// Application boundary for Gameplay: starts a match from a locked lobby, serves the current
/// question and records answers. Authorization decisions that depend on the caller's identity
/// are made here, where the validated subject is available.
/// </summary>
public interface IGameplayApplication
{
    /// <summary>Starts (or re-returns) the single match owned by a lobby.</summary>
    Task<(MatchResponse Match, bool Created)> CreateAsync(
        AuthenticatedActor actor,
        CreateMatchRequest request,
        CancellationToken cancellationToken);

    Task<MatchResponse> GetAsync(AuthenticatedActor actor, string matchId, CancellationToken cancellationToken);

    Task<CurrentQuestionResponse> GetCurrentQuestionAsync(AuthenticatedActor actor, string matchId, CancellationToken cancellationToken);

    Task<AnswerOutcomeResponse> SubmitAnswerAsync(
        AuthenticatedActor actor,
        string matchId,
        SubmitAnswerRequest request,
        CancellationToken cancellationToken);
}
