using QuizArena.Notification.Domain.Entities;
using QuizArena.Notification.Domain.Enums;
using QuizArena.Notification.Domain.Exceptions;

namespace QuizArena.Notification.Domain.Services;

/// <summary>
/// Pure rule deciding what to do when an event id is offered for storage a second time.
/// Equality is typed (type, recipient, match, outcome); serialization details never influence it.
/// </summary>
public static class EventReplayPolicy
{
    public static ReplayDecision Classify(Event? stored, Event incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        if (stored is null)
        {
            return ReplayDecision.New;
        }

        if (stored.EventId != incoming.EventId)
        {
            throw new ArgumentException("Stored and incoming events must share the same event id.", nameof(incoming));
        }

        return stored.HasSameContent(incoming)
            ? ReplayDecision.IdempotentReplay
            : ReplayDecision.Conflict;
    }

    /// <summary>
    /// Throws when the replay must be rejected, otherwise returns the event that should be reported.
    /// </summary>
    public static Event Resolve(Event? stored, Event incoming) =>
        Classify(stored, incoming) switch
        {
            ReplayDecision.New => incoming,
            ReplayDecision.IdempotentReplay => stored!,
            _ => throw new EventConflictException(incoming.EventId)
        };
}
