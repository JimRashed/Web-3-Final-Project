namespace QuizArena.Notification.Domain.Enums;

/// <summary>
/// Outcome of comparing an incoming event with the event already stored under the same id.
/// </summary>
public enum ReplayDecision
{
    /// <summary>No event exists under this id; persist the incoming event.</summary>
    New = 0,

    /// <summary>The stored event has the identical payload; return it unchanged.</summary>
    IdempotentReplay = 1,

    /// <summary>The stored event has a different payload; reject the request with a conflict.</summary>
    Conflict = 2
}
