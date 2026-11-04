namespace QuizArena.Notification.Domain.Enums;

/// <summary>
/// The only event types the notification service accepts from the Gameplay service.
/// </summary>
public enum EventType
{
    /// <summary>Sent to the guest when a match starts; payload carries only the match id.</summary>
    MatchCreated = 0,

    /// <summary>Sent to both players when a match finishes; payload carries the outcome.</summary>
    MatchCompleted = 1
}
