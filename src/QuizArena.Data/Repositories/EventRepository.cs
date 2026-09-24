using Microsoft.EntityFrameworkCore;
using QuizArena.Data.Context;
using QuizArena.Domain.Notification.Entities;

namespace QuizArena.Data.Repositories;

public class EventRepository : IEventRepository
{
    private readonly QuizArenaContext _db;

    public EventRepository(QuizArenaContext db) => _db = db;

    public async Task<EventLog> AddAsync(EventLog ev)
    {
        _db.EventLogs.Add(ev);
        await _db.SaveChangesAsync();
        return ev;
    }

    public async Task<List<EventLog>> GetByUserAsync(string userId)
    {
        return await _db.EventLogs
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<UserPref?> GetPrefAsync(string userId)
    {
        return await _db.UserPrefs.FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<UserPref> UpsertPrefAsync(UserPref pref)
    {
        var existing = await GetPrefAsync(pref.UserId);
        if (existing == null)
        {
            _db.UserPrefs.Add(pref);
        }
        else
        {
            existing.EmailEnabled = pref.EmailEnabled;
            existing.PushEnabled = pref.PushEnabled;
        }
        await _db.SaveChangesAsync();
        return pref;
    }
}