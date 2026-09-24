using QuizArena.Domain.Statistics.Entities;

namespace QuizArena.Data.Repositories;

public interface IStatisticsRepository
{
    Task AddAsync(MatchResult result);
    Task<List<MatchResult>> GetByUserAsync(Guid userId);
    Task<List<MatchResult>> GetAllAsync();
}