using QuizArena.Notification.Api.Contracts;

namespace QuizArena.Notification.Api.Services;

/// <summary>
/// Application boundary for the notification service: translates the HTTP contract into domain
/// operations and back. Ownership (caller subject == requested subject) is enforced by the API layer.
/// </summary>
public interface IEventApplication
{
    /// <summary>Persists an event produced by the Gameplay machine client; identical replays return the stored event.</summary>
    Task<EventResponse> PublishAsync(CreateEventRequest request, string issuer, CancellationToken cancellationToken);

    /// <summary>Events addressed to one recipient, newest first.</summary>
    Task<IReadOnlyList<EventResponse>> GetEventsAsync(string issuer, string subject, CancellationToken cancellationToken);

    /// <summary>Stored preferences, or the disabled default when the player never saved any.</summary>
    Task<UserPreferencesResponse> GetPreferencesAsync(string issuer, string subject, CancellationToken cancellationToken);

    Task<UserPreferencesResponse> UpdatePreferencesAsync(
        string issuer,
        string subject,
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken);
}
