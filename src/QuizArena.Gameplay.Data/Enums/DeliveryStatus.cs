namespace QuizArena.Gameplay.Data.Enums;

/// <summary>Lifecycle of one outbound delivery row.</summary>
public enum DeliveryStatus
{
    /// <summary>Waiting for its next attempt; the worker may claim it when due.</summary>
    Pending = 0,

    /// <summary>The receiver acknowledged the payload.</summary>
    Delivered = 1,

    /// <summary>Permanent failure or exhausted retries. Kept for an operator redrive; never deleted.</summary>
    Failed = 2,
}
