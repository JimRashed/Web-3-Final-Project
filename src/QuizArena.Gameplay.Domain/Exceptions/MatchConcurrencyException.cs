namespace QuizArena.Gameplay.Domain.Exceptions;

/// <summary>
/// Raised when a match was changed by another request between loading and saving it
/// (two players answering the same round at the same time). The caller should reload and retry.
/// </summary>
public sealed class MatchConcurrencyException : MatchDomainException
{
    public MatchConcurrencyException(string matchId)
        : base($"Match '{matchId}' was modified by another request. Reload the match and retry.")
    {
        MatchId = matchId;
    }

    public string MatchId { get; }
}
