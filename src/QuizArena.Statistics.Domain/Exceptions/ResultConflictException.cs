namespace QuizArena.Statistics.Domain.Exceptions;

/// <summary>
/// A result already exists for the same issuer, match and player with different content. Maps to 409.
/// </summary>
public sealed class ResultConflictException : StatisticsDomainException
{
    public ResultConflictException(string matchId, string userId)
        : base("A different result is already recorded for this match and player.")
    {
        MatchId = matchId;
        UserId = userId;
    }

    public string MatchId { get; }

    public string UserId { get; }
}
