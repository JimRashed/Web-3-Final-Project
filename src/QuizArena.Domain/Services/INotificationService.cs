using QuizArena.Domain.Contracts;

namespace QuizArena.Domain.Services;

public interface INotificationService
{
    Task<EventResponse> LogEventAsync(CreateEventRequest req);
    Task<List<EventResponse>> GetUserEventsAsync(string userId);
    Task<UserPrefDto> UpdatePrefAsync(string userId, UpdatePrefRequest req);
}

public record UserPrefDto(string UserId, bool EmailEnabled, bool PushEnabled);