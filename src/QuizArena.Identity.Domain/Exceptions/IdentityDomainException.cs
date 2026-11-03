namespace QuizArena.Identity.Domain.Exceptions;

/// <summary>
/// Base type for Identity rule violations. The Api maps these to safe ProblemDetails responses.
/// </summary>
public class IdentityDomainException : Exception
{
    public IdentityDomainException(string message)
        : base(message)
    {
    }
}
