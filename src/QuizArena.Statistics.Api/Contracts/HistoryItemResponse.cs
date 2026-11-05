using QuizArena.Statistics.Domain.Entities;

namespace QuizArena.Statistics.Api.Contracts;

/// <summary>One item of GET /api/players/{userId}/matches (schema History).</summary>
public sealed record HistoryItemResponse(string MatchId, int Score, bool Won, DateTime PlayedAt)
{
    public static HistoryItemResponse From(MatchResult result) =>
        new(result.MatchId, result.Score, result.Won, result.PlayedAt);
}
