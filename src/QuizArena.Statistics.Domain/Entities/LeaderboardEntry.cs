namespace QuizArena.Statistics.Domain.Entities;

/// <summary>One leaderboard row; <see cref="Rank"/> is the 1-based position after the deterministic ordering.</summary>
public sealed record LeaderboardEntry(int Rank, string UserId, int TotalScore, int Wins, int GamesPlayed);
