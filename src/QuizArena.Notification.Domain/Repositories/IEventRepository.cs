using QuizArena.Notification.Domain.Entities;

namespace QuizArena.Notification.Domain.Repositories;

/// <summary>
/// Persistence port for <see cref="Event"/>, implemented by QuizArena.Notification.Data.
/// Events are append-only and keyed by the producer-generated <see cref="Event.EventId"/>.
/// </summary>
public interface IEventRepository
{
    /// <summary>Read-only lookup by idempotency key; returns null when the event is unknown.</summary>
    Task<Event?> FindAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores <paramref name="candidate"/> and returns the event that is now stored:
    /// the candidate for a new event, the existing row for an identical replay.
    /// Throws <c>EventConflictException</c> when the id already exists with different content.
    /// </summary>
    Task<Event> AddAsync(Event candidate, CancellationToken cancellationToken);

    /// <summary>Events for one recipient, newest first.</summary>
    Task<IReadOnlyList<Event>> GetByUserAsync(string issuer, string userId, CancellationToken cancellationToken);
}
