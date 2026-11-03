namespace QuizArena.Identity.Domain.Exceptions;

/// <summary>
/// A profile value breaks an invariant (blank, too long, not UTC, ...). Maps to 400 at the API boundary.
/// </summary>
public sealed class InvalidProfileException : IdentityDomainException
{
    public InvalidProfileException(string field, string message)
        : base(message)
    {
        Field = field;
    }

    /// <summary>Name of the offending field, for field-level validation messages.</summary>
    public string Field { get; }
}
