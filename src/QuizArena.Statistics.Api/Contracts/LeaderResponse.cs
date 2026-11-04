using QuizArena.Statistics.Domain.Entities;

namespace QuizArena.Statistics.Api.Contracts;

/// <summary>One row of GET /api/leaderboard (schema Leader); the array order is the rank.</summary>
public sealed record LeaderResponse(string UserId, int TotalScore, int Wins)
{
    public static LeaderResponse From(LeaderboardEntry entry) => new(entry.UserId, entry.TotalScore, entry.Wins);
}
