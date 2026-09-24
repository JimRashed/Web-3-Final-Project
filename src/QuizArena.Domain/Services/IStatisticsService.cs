using QuizArena.Domain.Contracts;

namespace QuizArena.Domain.Services;

public interface IStatisticsService
{
    Task RecordResultAsync(RecordResultRequest request);
    Task<PlayerStatsDto> GetStatsAsync(Guid userId);
    Task<List<MatchHistoryDto>> GetHistoryAsync(Guid userId);
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int top);
}