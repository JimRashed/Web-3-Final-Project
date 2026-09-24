using Microsoft.EntityFrameworkCore;
using QuizArena.Data.Context;
using QuizArena.Domain.Gameplay.Entities;

namespace QuizArena.Data.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly QuizArenaContext _db;

    public MatchRepository(QuizArenaContext db) => _db = db;

    public async Task<ActiveMatch> CreateAsync(ActiveMatch match)
    {
        _db.ActiveMatches.Add(match);
        await _db.SaveChangesAsync();
        return match;
    }

    public async Task<ActiveMatch?> GetAsync(string matchId)
    {
        return await _db.ActiveMatches.FirstOrDefaultAsync(m => m.MatchId == matchId);
    }

    public async Task<ActiveMatch> UpdateAsync(ActiveMatch match)
    {
        _db.ActiveMatches.Update(match);
        await _db.SaveChangesAsync();
        return match;
    }
}