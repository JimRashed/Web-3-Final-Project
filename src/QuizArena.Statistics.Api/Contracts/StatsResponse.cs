using QuizArena.Statistics.Domain.Entities;

namespace QuizArena.Statistics.Api.Contracts;

/// <summary>GET /api/players/{userId}/stats (schema Stats).</summary>
public sealed record StatsResponse(string UserId, int GamesPlayed, int Wins, int TotalScore)
{
    public static StatsResponse From(PlayerTotals totals) =>
        new(totals.UserId, totals.GamesPlayed, totals.Wins, totals.TotalScore);
}
