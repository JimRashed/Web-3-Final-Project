namespace QuizArena.Statistics.Domain.Entities;

/// <summary>Aggregated totals for one player. A player with no results has all zeros.</summary>
public sealed record PlayerTotals(string UserId, int GamesPlayed, int Wins, int TotalScore);
