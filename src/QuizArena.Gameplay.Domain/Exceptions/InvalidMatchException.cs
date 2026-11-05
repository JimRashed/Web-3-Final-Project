namespace QuizArena.Gameplay.Domain.Exceptions;

/// <summary>
/// Raised when a match or question snapshot cannot be created from the supplied data.
/// </summary>
public sealed class InvalidMatchException : MatchDomainException
{
    public InvalidMatchException(string message) : base(message)
    {
    }
}
