using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Notification.Data.Context;
using QuizArena.Notification.Domain.Entities;
using QuizArena.Notification.Domain.Repositories;

namespace QuizArena.Notification.Data.Repositories;

public sealed class UserPreferenceRepository(NotificationDbContext context) : IUserPreferenceRepository
{
    // SQL Server duplicate-key error numbers (unique index / primary key).
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;

    public Task<UserPreference?> FindAsync(string issuer, string subject, CancellationToken cancellationToken) =>
        context.UserPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(preference => preference.Issuer == issuer && preference.Subject == subject, cancellationToken);

    public async Task<UserPreference> UpsertAsync(UserPreference preference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preference);

        var existing = await context.UserPreferences
            .SingleOrDefaultAsync(
                stored => stored.Issuer == preference.Issuer && stored.Subject == preference.Subject,
                cancellationToken);

        if (existing is null)
        {
            context.UserPreferences.Add(preference);
        }
        else
        {
            existing.Update(preference.EmailEnabled, preference.PushEnabled);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return existing ?? preference;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // A concurrent request created the row first: apply the update to the stored one.
            context.Entry(preference).State = EntityState.Detached;
            var winner = await context.UserPreferences
                .SingleOrDefaultAsync(
                    stored => stored.Issuer == preference.Issuer && stored.Subject == preference.Subject,
                    cancellationToken)
                ?? throw new InvalidOperationException("Preference insert conflicted but no stored row was found.", exception);

            winner.Update(preference.EmailEnabled, preference.PushEnabled);
            await context.SaveChangesAsync(cancellationToken);
            return winner;
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or PrimaryKeyViolation };
}
