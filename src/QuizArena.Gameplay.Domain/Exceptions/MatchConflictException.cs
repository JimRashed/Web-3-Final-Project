namespace QuizArena.Gameplay.Domain.Exceptions;

/// <summary>
/// Raised when an operation conflicts with the match's current state (HTTP 409).
/// </summary>
public sealed class MatchConflictException : MatchDomainException
{
    public MatchConflictException(MatchConflictReason reason, string message) : base(message)
    {
        Reason = reason;
    }

    public MatchConflictReason Reason { get; }
}

public enum MatchConflictReason
{
    MatchFinished = 0,
    DuplicateAnswer = 1,
    StaleQuestion = 2
}
