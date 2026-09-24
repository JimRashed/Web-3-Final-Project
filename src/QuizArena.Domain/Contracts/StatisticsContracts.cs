namespace QuizArena.Domain.Contracts;

public record RecordResultRequest(Guid MatchId, Guid UserId, int Score, bool Won);
public record PlayerStatsDto(Guid UserId, int GamesPlayed, int Wins, int TotalScore);
public record MatchHistoryDto(Guid MatchId, int Score, bool Won, DateTime PlayedAt);
public record LeaderboardEntryDto(Guid UserId, int TotalScore, int Wins);