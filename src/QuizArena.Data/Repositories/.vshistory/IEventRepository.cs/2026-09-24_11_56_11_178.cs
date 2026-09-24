uusing QuizArena.Domain.Notification.Entities;

namespace QuizArena.Data.Repositories;

public interface IEventRepository
{
    Task<EventLog> AddAsync(EventLog ev);
    Task<List<EventLog>> GetByUserAsync(string userId);
    Task<UserPref?> GetPrefAsync(string userId);
    Task<UserPref> UpsertPrefAsync(UserPref pref);
}