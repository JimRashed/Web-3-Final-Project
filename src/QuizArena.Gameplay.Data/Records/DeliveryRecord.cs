using QuizArena.Gameplay.Data.Enums;

namespace QuizArena.Gameplay.Data.Records;

/// <summary>
/// One durable outbound delivery (transactional outbox row). Everything needed to replay the
/// message after a restart is stored here — the payload and identity are frozen at commit time.
/// </summary>
public sealed class DeliveryRecord
{
    public Guid Id { get; set; }

    /// <summary>Logical receiver: <c>statistics</c> or <c>notification</c>.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Wire operation and version, so a stored row can outlive a code change.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Immutable canonical request body.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Receiver-facing deduplication key; repeats keep the same value.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public DeliveryStatus Status { get; set; }

    /// <summary>Attempts started so far; a claimed row is already counting its current attempt.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Earliest UTC time the next attempt may start.</summary>
    public DateTime NextAttemptAt { get; set; }

    /// <summary>
    /// Set while a worker owns the row. A crashed worker leaves the lease behind; once it expires
    /// another worker claims the row again, which is what makes restarts safe.
    /// </summary>
    public DateTime? LeaseUntil { get; set; }

    /// <summary>Last safe error code or description (no payload, token or response body).</summary>
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }
}
