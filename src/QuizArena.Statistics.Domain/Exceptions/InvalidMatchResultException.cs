namespace QuizArena.Statistics.Domain.Exceptions;

/// <summary>
/// A result or query value breaks an invariant (blank id, negative score, non-UTC time, ...). Maps to 400.
/// </summary>
public sealed class InvalidMatchResultException : StatisticsDomainException
{
    public InvalidMatchResultException(string field, string message)
        : base(message)
    {
        Field = field;
    }

    /// <summary>Name of the offending field, for field-level validation messages.</summary>
    public string Field { get; }
}
