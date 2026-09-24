using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Notification.Entities;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Services;

public class NotificationService : INotificationService
{
    private readonly IEventRepository _repo;

    public NotificationService(IEventRepository repo) => _repo = repo;

    public async Task<EventResponse> LogEventAsync(CreateEventRequest req)
    {
        var ev = new EventLog
        {
            Type = req.Type,
            UserId = req.UserId,
            PayloadJson = req.PayloadJson,
            CreatedAt = DateTime.UtcNow
        };

        var saved = await _repo.AddAsync(ev);

        return new EventResponse(
            saved.Id, saved.Type, saved.UserId, saved.PayloadJson, saved.CreatedAt);
    }

    public async Task<List<EventResponse>> GetUserEventsAsync(string userId)
    {
        var events = await _repo.GetByUserAsync(userId);
        return events
            .Select(e => new EventResponse(e.Id, e.Type, e.UserId, e.PayloadJson, e.CreatedAt))
            .ToList();
    }

    public async Task<UserPrefDto> UpdatePrefAsync(string userId, UpdatePrefRequest req)
    {
        var pref = new UserPref
        {
            UserId = userId,
            EmailEnabled = req.EmailEnabled,
            PushEnabled = req.PushEnabled
        };

        var saved = await _repo.UpsertPrefAsync(pref);
        return new UserPrefDto(saved.UserId, saved.EmailEnabled, saved.PushEnabled);
    }
}