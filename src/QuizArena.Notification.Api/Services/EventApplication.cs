using QuizArena.Notification.Api.Contracts;
using QuizArena.Notification.Domain.Entities;
using QuizArena.Notification.Domain.Exceptions;
using QuizArena.Notification.Domain.Repositories;

namespace QuizArena.Notification.Api.Services;

public sealed class EventApplication(
    IEventRepository events,
    IUserPreferenceRepository preferences) : IEventApplication
{
    public async Task<EventResponse> PublishAsync(CreateEventRequest request, string issuer, CancellationToken cancellationToken)
    {
        var candidate = BuildEvent(request, issuer);
        var stored = await events.AddAsync(candidate, cancellationToken);
        return EventResponse.FromDomain(stored);
    }

    public async Task<IReadOnlyList<EventResponse>> GetEventsAsync(string issuer, string subject, int page, int pageSize, CancellationToken cancellationToken)
    {
        InvalidPagingException.ThrowIfInvalid(page, pageSize);
        var stored = await events.GetByUserAsync(issuer, subject, page, pageSize, cancellationToken);
        return stored.Select(EventResponse.FromDomain).ToList();
    }

    public async Task<UserPreferencesResponse> GetPreferencesAsync(string issuer, string subject, CancellationToken cancellationToken)
    {
        var stored = await preferences.FindAsync(issuer, subject, cancellationToken);

        // A player who never saved preferences is treated as opted out of both channels.
        var effective = stored ?? UserPreference.CreateDefault(issuer, subject);
        return new UserPreferencesResponse(effective.EmailEnabled, effective.PushEnabled);
    }

    public async Task<UserPreferencesResponse> UpdatePreferencesAsync(
        string issuer,
        string subject,
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var candidate = UserPreference.Rehydrate(issuer, subject, request.EmailEnabled, request.PushEnabled);
        var stored = await preferences.UpsertAsync(candidate, cancellationToken);
        return new UserPreferencesResponse(stored.EmailEnabled, stored.PushEnabled);
    }

    private static Event BuildEvent(CreateEventRequest request, string issuer)
    {
        if (request.Payload is null || string.IsNullOrWhiteSpace(request.Payload.MatchId))
        {
            throw new InvalidEventException("The event payload requires matchId.");
        }

        var createdAt = DateTime.UtcNow;

        return request.Type switch
        {
            "MATCH_CREATED" when request.Payload.WinnerUserId is null && request.Payload.Draw is null =>
                Event.CreateMatchCreated(request.EventId, issuer, request.UserId, request.Payload.MatchId, createdAt),

            "MATCH_CREATED" =>
                throw new InvalidEventException("MATCH_CREATED accepts only matchId."),

            "MATCH_COMPLETED" when request.Payload.Draw is not null =>
                Event.CreateMatchCompleted(
                    request.EventId,
                    issuer,
                    request.UserId,
                    request.Payload.MatchId,
                    request.Payload.WinnerUserId,
                    request.Payload.Draw.Value,
                    createdAt),

            "MATCH_COMPLETED" =>
                throw new InvalidEventException("MATCH_COMPLETED requires matchId, winnerUserId and draw."),

            _ => throw new InvalidEventException($"Unsupported event type '{request.Type}'.")
        };
    }
}
