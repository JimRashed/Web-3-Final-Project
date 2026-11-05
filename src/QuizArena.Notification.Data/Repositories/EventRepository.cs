using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Notification.Data.Context;
using QuizArena.Notification.Domain.Entities;
using QuizArena.Notification.Domain.Repositories;
using QuizArena.Notification.Domain.Services;

namespace QuizArena.Notification.Data.Repositories;

public sealed class EventRepository(NotificationDbContext context) : IEventRepository
{
    // SQL Server duplicate-key error numbers (unique index / primary key).
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;

    public Task<Event?> FindAsync(Guid eventId, CancellationToken cancellationToken) =>
        context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(@event => @event.EventId == eventId, cancellationToken);

    public async Task<Event> AddAsync(Event candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var existing = await FindAsync(candidate.EventId, cancellationToken);
        if (existing is not null)
        {
            // Identical content returns the stored event; different content throws a conflict.
            return EventReplayPolicy.Resolve(existing, candidate);
        }

        context.Events.Add(candidate);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return candidate;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // A concurrent delivery stored this id first: decide against the winner.
            context.Entry(candidate).State = EntityState.Detached;
            var winner = await FindAsync(candidate.EventId, cancellationToken)
                ?? throw new InvalidOperationException("Event insert conflicted but no stored event was found.", exception);
            return EventReplayPolicy.Resolve(winner, candidate);
        }
    }

    public async Task<IReadOnlyList<Event>> GetByUserAsync(
        string issuer,
        string userId,
        CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Where(@event => @event.Issuer == issuer && @event.UserId == userId)
            .OrderByDescending(@event => @event.CreatedAt)
            .ToListAsync(cancellationToken);

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or PrimaryKeyViolation };
}
