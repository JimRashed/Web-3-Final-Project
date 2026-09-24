using Microsoft.EntityFrameworkCore;
using QuizArena.Data.Context;
using QuizArena.Domain.Statistics.Entities;

namespace QuizArena.Data.Repositories;

public class StatisticsRepository : IStatisticsRepository
{
    private readonly StatisticsContext _context;

    public StatisticsRepository(StatisticsContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MatchResult result)
    {
        _context.MatchResults.Add(result);
        await _context.SaveChangesAsync();
    }

    public Task<List<MatchResult>> GetByUserAsync(Guid userId) =>
        _context.MatchResults
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.PlayedAt)
            .ToListAsync();

    public Task<List<MatchResult>> GetAllAsync() =>
        _context.MatchResults.ToListAsync();
}