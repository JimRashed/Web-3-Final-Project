using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;
using QuizArena.Domain.Statistics.Entities;

namespace QuizArena.Statistics.Api.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IStatisticsRepository _repo;

    public StatisticsService(IStatisticsRepository repo)
    {
        _repo = repo;
    }

    public async Task RecordResultAsync(RecordResultRequest request)
    {
        var result = new MatchResult
        {
            MatchId = request.MatchId,
            UserId = request.UserId,
            Score = request.Score,
            Won = request.Won,
            PlayedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(result);
    }

    public async Task<PlayerStatsDto> GetStatsAsync(Guid userId)
    {
        var results = await _repo.GetByUserAsync(userId);
        return new PlayerStatsDto(
            userId,
            results.Count,
            results.Count(r => r.Won),
            results.Sum(r => r.Score));
    }

    public async Task<List<MatchHistoryDto>> GetHistoryAsync(Guid userId)
    {
        var results = await _repo.GetByUserAsync(userId);
        return results
            .Select(r => new MatchHistoryDto(r.MatchId, r.Score, r.Won, r.PlayedAt))
            .ToList();
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int top)
    {
        var results = await _repo.GetAllAsync();
        return results
            .GroupBy(r => r.UserId)
            .Select(g => new LeaderboardEntryDto(g.Key, g.Sum(r => r.Score), g.Count(r => r.Won)))
            .OrderByDescending(e => e.TotalScore)
            .Take(top)
            .ToList();
    }
}