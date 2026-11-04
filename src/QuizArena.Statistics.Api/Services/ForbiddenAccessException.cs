namespace QuizArena.Statistics.Api.Services;

/// <summary>A valid caller asked for another player's self-only data. Maps to 403.</summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("You can only view your own statistics.")
    {
    }
}
