namespace QuizArena.Gameplay.Domain.Exceptions;

/// <summary>
/// Base type for violations of the gameplay bounded context's business rules.
/// </summary>
public class MatchDomainException : Exception
{
    public MatchDomainException(string message) : base(message)
    {
    }
}
