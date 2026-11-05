namespace QuizArena.Statistics.Domain.Exceptions;

/// <summary>
/// Base type for Statistics rule violations. The Api maps these to safe ProblemDetails responses.
/// </summary>
public class StatisticsDomainException : Exception
{
    public StatisticsDomainException(string message)
        : base(message)
    {
    }
}
