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

    /// <summary>Marks the row delivered; the payload is kept as evidence.</summary>
    Task DeliveredAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken);

    /// <summary>Stores the next attempt time and a safe error code, then releases the lease.</summary>
    Task RescheduleAsync(Guid id, DateTime nextAttemptAtUtc, string error, CancellationToken cancellationToken);

    /// <summary>Terminal state a human must resolve; the row is never deleted.</summary>
    Task FailedAsync(Guid id, string error, CancellationToken cancellationToken);

    /// <summary>Operator redrive: a failed row becomes pending again with a fresh attempt series.</summary>
    Task<bool> RedriveAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken);
}
