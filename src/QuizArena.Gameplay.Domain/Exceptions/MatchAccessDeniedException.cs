namespace QuizArena.Gameplay.Domain.Exceptions;

/// <summary>
/// Raised when a caller is not a member of the match (mapped to HTTP 403 by the API layer).
/// </summary>
public sealed class MatchAccessDeniedException : MatchDomainException
{
    public MatchAccessDeniedException(string matchId, string userId)
        : base($"User '{userId}' is not a member of match '{matchId}'.")
    {
        MatchId = matchId;
        UserId = userId;
    }

    public string MatchId { get; }

    public string UserId { get; }
}
