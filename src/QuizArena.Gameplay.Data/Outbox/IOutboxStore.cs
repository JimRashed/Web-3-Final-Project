using QuizArena.Gameplay.Data.Records;

namespace QuizArena.Gameplay.Data.Outbox;

/// <summary>
/// The persistence side of the delivery outbox. Claiming is a short database transaction that marks
/// the rows it returns with a lease, so two workers (or two processes after a restart) never send
/// the same row at the same time.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Atomically leases up to <paramref name="batchSize"/> due pending rows and returns them.
    /// A row whose lease expired (crashed worker) is claimable again without resetting its attempt count.
    /// </summary>
    Task<IReadOnlyList<DeliveryRecord>> ClaimAsync(int batchSize, TimeSpan lease, DateTime utcNow, CancellationToken cancellationToken);

    // The completion writes below are fenced on the lease the caller claimed (claimedLeaseUntil). If the lease
    // expired and another worker reclaimed the row, the late write changes nothing and returns false, so a slow
    // worker can never overwrite the current owner's result.

    /// <summary>Marks the row delivered; the payload is kept as evidence. False when the lease was lost.</summary>
    Task<bool> DeliveredAsync(Guid id, DateTime claimedLeaseUntil, DateTime utcNow, CancellationToken cancellationToken);

    /// <summary>Stores the next attempt time and a safe error code, then releases the lease. False when the lease was lost.</summary>
    Task<bool> RescheduleAsync(Guid id, DateTime claimedLeaseUntil, DateTime nextAttemptAtUtc, string error, CancellationToken cancellationToken);

    /// <summary>Terminal state a human must resolve; the row is never deleted. False when the lease was lost.</summary>
    Task<bool> FailedAsync(Guid id, DateTime claimedLeaseUntil, string error, CancellationToken cancellationToken);

    /// <summary>Operator redrive: a failed row becomes pending again with a fresh attempt series.</summary>
    Task<bool> RedriveAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken);
}
