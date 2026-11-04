using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Identity.Data.Context;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Repositories;

namespace QuizArena.Identity.Data.Repositories;

public sealed class PlayerProfileRepository(IdentityDbContext context) : IPlayerProfileRepository
{
    // SQL Server duplicate-key error numbers (unique index / primary key).
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;

    public Task<PlayerProfile?> FindByIdentityAsync(string issuer, string subject, CancellationToken cancellationToken) =>
        context.Set<PlayerProfile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.Issuer == issuer && profile.Subject == subject, cancellationToken);

    public async Task<PlayerProfile> AddIfAbsentAsync(PlayerProfile candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var existing = await FindByIdentityAsync(candidate.Issuer, candidate.Subject, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        context.Add(candidate);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return candidate;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // A concurrent request created the profile first; return the stored one.
            context.Entry(candidate).State = EntityState.Detached;
            return await FindByIdentityAsync(candidate.Issuer, candidate.Subject, cancellationToken)
                ?? throw new InvalidOperationException("Profile insert conflicted but no stored profile was found.", exception);
        }
    }

    public async Task<PlayerProfile?> RenameAsync(
        string issuer,
        string subject,
        string displayName,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        var profile = await context.Set<PlayerProfile>()
            .SingleOrDefaultAsync(p => p.Issuer == issuer && p.Subject == subject, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        if (profile.Rename(displayName, updatedAt))
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return profile;
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or PrimaryKeyViolation };
}
