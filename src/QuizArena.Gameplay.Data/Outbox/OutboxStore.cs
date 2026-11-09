using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using QuizArena.Gameplay.Data.Context;
using QuizArena.Gameplay.Data.Enums;
using QuizArena.Gameplay.Data.Records;

namespace QuizArena.Gameplay.Data.Outbox;

public sealed class OutboxStore(GameplayDbContext context) : IOutboxStore
{
    public async Task<IReadOnlyList<DeliveryRecord>> ClaimAsync(
        int batchSize,
        TimeSpan lease,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
        {
            return [];
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // UPDLOCK + READPAST: concurrent claimers skip each other's rows instead of blocking, and
        // the lock is held until this transaction commits, so the lease update is not racy.
        var ids = await context.Deliveries
            .FromSqlInterpolated($"""
                SELECT TOP ({batchSize}) *
                FROM Deliveries WITH (UPDLOCK, ROWLOCK, READPAST)
                WHERE Status = {(int)DeliveryStatus.Pending}
                  AND NextAttemptAt <= {utcNow}
                  AND (LeaseUntil IS NULL OR LeaseUntil <= {utcNow})
                ORDER BY NextAttemptAt
                """)
            .Select(record => record.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }

        var leaseUntil = utcNow + lease;
        await context.Deliveries
            .Where(record => ids.Contains(record.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(record => record.LeaseUntil, leaseUntil)
                    .SetProperty(record => record.AttemptCount, record => record.AttemptCount + 1),
                cancellationToken);

        var claimed = await context.Deliveries
            .AsNoTracking()
            .Where(record => ids.Contains(record.Id))
            .OrderBy(record => record.NextAttemptAt)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    public Task<bool> DeliveredAsync(Guid id, DateTime claimedLeaseUntil, DateTime utcNow, CancellationToken cancellationToken) =>
        CompleteAsync(id, claimedLeaseUntil, setters => setters
            .SetProperty(record => record.Status, DeliveryStatus.Delivered)
            .SetProperty(record => record.DeliveredAt, utcNow)
            .SetProperty(record => record.LeaseUntil, (DateTime?)null)
            .SetProperty(record => record.LastError, (string?)null), cancellationToken);

    public Task<bool> RescheduleAsync(Guid id, DateTime claimedLeaseUntil, DateTime nextAttemptAtUtc, string error, CancellationToken cancellationToken)
    {
        var lastError = Truncate(error);
        return CompleteAsync(id, claimedLeaseUntil, setters => setters
            .SetProperty(record => record.Status, DeliveryStatus.Pending)
            .SetProperty(record => record.NextAttemptAt, nextAttemptAtUtc)
            .SetProperty(record => record.LeaseUntil, (DateTime?)null)
            .SetProperty(record => record.LastError, lastError), cancellationToken);
    }

    public Task<bool> FailedAsync(Guid id, DateTime claimedLeaseUntil, string error, CancellationToken cancellationToken)
    {
        var lastError = Truncate(error);
        return CompleteAsync(id, claimedLeaseUntil, setters => setters
            .SetProperty(record => record.Status, DeliveryStatus.Failed)
            .SetProperty(record => record.LeaseUntil, (DateTime?)null)
            .SetProperty(record => record.LastError, lastError), cancellationToken);
    }

    public async Task<bool> RedriveAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken)
    {
        var affected = await context.Deliveries
            .Where(record => record.Id == id && record.Status == DeliveryStatus.Failed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(record => record.Status, DeliveryStatus.Pending)
                    .SetProperty(record => record.AttemptCount, 0)
                    .SetProperty(record => record.NextAttemptAt, utcNow)
                    .SetProperty(record => record.LeaseUntil, (DateTime?)null),
                cancellationToken);

        return affected == 1;
    }

    private async Task<bool> CompleteAsync(
        Guid id,
        DateTime claimedLeaseUntil,
        Action<UpdateSettersBuilder<DeliveryRecord>> change,
        CancellationToken cancellationToken)
    {
        // Fence: only the holder of the current lease may complete the row. The check and the write are one
        // UPDATE ... WHERE statement, so a reclaim cannot slip in between a separate read and save.
        var affected = await context.Deliveries
            .Where(row => row.Id == id && row.Status == DeliveryStatus.Pending && row.LeaseUntil == claimedLeaseUntil)
            .ExecuteUpdateAsync(change, cancellationToken);

        return affected == 1;
    }

    private static string Truncate(string error) =>
        error.Length <= 400 ? error : error[..400];
}
