namespace QuizArena.Notification.Domain.Exceptions;

/// <summary>
/// Raised when an event id is replayed with a different, immutable payload.
/// </summary>
public sealed class EventConflictException : NotificationDomainException
{
    public EventConflictException(Guid eventId)
        : base($"Event '{eventId}' already exists with a different payload.")
    {
        EventId = eventId;
    }

    public Guid EventId { get; }
}
