using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using QuizArena.Identity.Data.Configurations;
using QuizArena.Identity.Data.Context;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Exceptions;
using QuizArena.Identity.Domain.Repositories;

namespace QuizArena.Identity.Data.Repositories;

/// <summary>
/// Stores profile pictures in the Identity database. A replacement is one explicit transaction guarded by the row's
/// rowversion, so readers see the old picture or the new one and the first committer wins (SEC-07).
/// </summary>
/// <remarks>
/// <para>
/// <b>Reads are validated.</b> <see cref="GetAsync"/> maps the row through <see cref="ProfilePicture.Rehydrate"/> and
/// <see cref="GetInfoAsync"/> through <see cref="ProfilePicture.RehydrateInfo"/>; a row that breaks an invariant throws
/// <see cref="ProfilePictureCorruptException"/> instead of being returned. <see cref="ReplaceAsync"/> checks the loaded
/// row the same way before replacing it.
/// </para>
/// <para>
/// <b>Unknown commit outcome.</b> SQL Server can commit the transaction and then lose the acknowledgement, so an
/// exception from the commit step does not prove the write was rolled back. When <c>CommitAsync</c> throws, the store
/// disposes the transaction and re-reads the row with a fresh no-tracking query (without the caller's token, so a
/// cancelled caller still gets a resolved outcome), then compares it with the write it attempted:
/// </para>
/// <list type="bullet">
/// <item>the row carries the rowversion this call's UPDATE/INSERT produced, the expected version, content type and the
/// same SHA-256 of the content: the commit happened, and the call returns the new reference as a success;</item>
/// <item>the row is still the prior state (the same prior rowversion, or no row for a first upload): the commit did
/// not happen, the original commit exception is rethrown, nothing changed and the caller may retry;</item>
/// <item>the profile no longer exists: <see cref="PlayerProfileNotFoundException"/>;</item>
/// <item>anything else (another request changed the row): <see cref="ProfilePictureConflictException"/>.</item>
/// </list>
/// <para>
/// Guarantee: one <see cref="ReplaceAsync"/> call increments the stored version at most once, and it never reports a
/// failure for a write that committed and is still the current row, so the caller has no failure to retry. The store
/// never retries internally and does not de-duplicate separate calls: a second call is a new upload and gets the next
/// version. If the verification read itself fails, the original commit exception is rethrown and the outcome stays
/// unknown to the caller.
/// The guarantee also assumes locking READ COMMITTED: under READ_COMMITTED_SNAPSHOT (the Azure SQL default), a broken
/// connection mid-commit can let the verification read on a new connection see the prior row while the commit is
/// still completing, so the call rethrows and a client retry would add one more version.
/// </para>
/// <para>
/// <b>Failure cleanup.</b> On any failure only the picture entry this call tracked is detached; other entries in the
/// scoped context are left alone. Identity does not enable <c>EnableRetryOnFailure</c>. If it ever does, the explicit
/// transaction in <see cref="ReplaceAsync"/> must move inside <c>Database.CreateExecutionStrategy().ExecuteAsync</c>.
/// </para>
/// </remarks>
public sealed class ProfilePictureStore(IdentityDbContext context) : IProfilePictureStore
{
    // SQL Server error numbers: duplicate key (unique index / primary key) and foreign-key violation.
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;
    private const int ForeignKeyViolation = 547;

    public async Task<ProfilePictureInfo?> GetInfoAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var row = await context.Set<ProfilePicture>()
            .AsNoTracking()
            .Where(picture => picture.ProfileId == profileId)
            .Select(picture => new { picture.ContentType, picture.Version, picture.UpdatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : Validated(() => ProfilePicture.RehydrateInfo(row.ContentType, row.Version, row.UpdatedAt));
    }

    public async Task<ProfilePicture?> GetAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var row = await context.Set<ProfilePicture>()
            .AsNoTracking()
            .Where(picture => picture.ProfileId == profileId)
            .Select(picture => new { picture.ProfileId, picture.ContentType, picture.Content, picture.Version, picture.UpdatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : Validated(() => ProfilePicture.Rehydrate(row.ProfileId, row.ContentType, row.Content, row.Version, row.UpdatedAt));
    }

    public async Task<ProfilePictureInfo> ReplaceAsync(
        Guid profileId,
        string contentType,
        byte[] content,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        EntityEntry<ProfilePicture>? entry = null;
        var succeeded = false;
        try
        {
            AttemptedWrite attempted;
            ExceptionDispatchInfo commitFailure;
            await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
            {
                var picture = await context.Set<ProfilePicture>()
                    .SingleOrDefaultAsync(p => p.ProfileId == profileId, cancellationToken);
                byte[]? priorRowVersion = null;

                // Domain validation runs before any SQL write.
                if (picture is null)
                {
                    picture = ProfilePicture.Create(profileId, contentType, content, updatedAt);
                    entry = context.Add(picture);
                }
                else
                {
                    entry = context.Entry(picture);
                    Validated(() => ProfilePicture.Rehydrate(picture.ProfileId, picture.ContentType, picture.Content, picture.Version, picture.UpdatedAt));
                    priorRowVersion = RowVersionOf(entry);
                    picture.Replace(contentType, content, updatedAt);
                }

                // The UPDATE carries WHERE RowVersion = @original; a concurrent committed replace makes it affect 0 rows.
                await context.SaveChangesAsync(cancellationToken);
                attempted = new AttemptedWrite(picture.ToInfo(), SHA256.HashData(picture.Content), RowVersionOf(entry), priorRowVersion);

                try
                {
                    await transaction.CommitAsync(cancellationToken);
                    succeeded = true;
                    return attempted.Info;
                }
                catch (Exception exception)
                {
                    commitFailure = ExceptionDispatchInfo.Capture(exception);
                }
            }

            // The transaction is disposed: rolled back if the commit never reached SQL Server, no effect if it did.
            var committed = await ResolveCommitOutcomeAsync(profileId, attempted, commitFailure);
            succeeded = true;
            return committed;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // 0 rows affected: a concurrent replace won, or the profile (and, by cascade, its picture) was deleted.
            if (!await ProfileExistsAsync(profileId))
            {
                throw new PlayerProfileNotFoundException(exception);
            }

            throw new ProfilePictureConflictException(exception);
        }
        catch (DbUpdateException exception) when (HasSqlError(exception, UniqueIndexViolation, PrimaryKeyViolation))
        {
            // A concurrent first upload inserted the row first.
            throw new ProfilePictureConflictException(exception);
        }
        catch (DbUpdateException exception) when (HasSqlError(exception, ForeignKeyViolation))
        {
            throw new PlayerProfileNotFoundException(exception);
        }
        finally
        {
            if (!succeeded && entry is not null)
            {
                // Only this call's picture entry; the next attempt in this scope reloads it from the database.
                entry.State = EntityState.Detached;
            }
        }
    }

    public async Task<bool> DeleteAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var rows = await context.Set<ProfilePicture>()
            .Where(picture => picture.ProfileId == profileId)
            .ExecuteDeleteAsync(cancellationToken);
        return rows > 0;
    }

    private async Task<ProfilePictureInfo> ResolveCommitOutcomeAsync(Guid profileId, AttemptedWrite attempted, ExceptionDispatchInfo commitFailure)
    {
        StoredRow? stored = null;
        try
        {
            stored = await context.Set<ProfilePicture>()
                .AsNoTracking()
                .Where(picture => picture.ProfileId == profileId)
                .Select(picture => new StoredRow(
                    picture.ContentType,
                    picture.Version,
                    picture.Content,
                    EF.Property<byte[]>(picture, ProfilePictureConfiguration.RowVersionProperty)))
                .SingleOrDefaultAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // The outcome cannot be determined; surface the original commit failure.
            commitFailure.Throw();
        }

        if (stored is not null
            && stored.RowVersion.AsSpan().SequenceEqual(attempted.RowVersion)
            && stored.Version == attempted.Info.Version
            && string.Equals(stored.ContentType, attempted.Info.ContentType, StringComparison.Ordinal)
            && SHA256.HashData(stored.Content).AsSpan().SequenceEqual(attempted.ContentHash))
        {
            return attempted.Info;
        }

        var unchanged = attempted.PriorRowVersion is null
            ? stored is null
            : stored is not null && stored.RowVersion.AsSpan().SequenceEqual(attempted.PriorRowVersion);
        if (unchanged)
        {
            // Not committed: the prior state is intact and the caller may retry.
            commitFailure.Throw();
        }

        if (!await ProfileExistsAsync(profileId))
        {
            throw new PlayerProfileNotFoundException(commitFailure.SourceException);
        }

        throw new ProfilePictureConflictException(commitFailure.SourceException);
    }

    private Task<bool> ProfileExistsAsync(Guid profileId) =>
        context.UserProfiles.AsNoTracking().AnyAsync(profile => profile.Id == profileId, CancellationToken.None);

    private static byte[] RowVersionOf(EntityEntry<ProfilePicture> entry) =>
        entry.Property<byte[]>(ProfilePictureConfiguration.RowVersionProperty).CurrentValue;

    private static T Validated<T>(Func<T> rehydrate)
    {
        try
        {
            return rehydrate();
        }
        catch (InvalidProfileException exception)
        {
            throw new ProfilePictureCorruptException(exception);
        }
    }

    private static bool HasSqlError(DbUpdateException exception, params int[] numbers) =>
        exception.InnerException is SqlException sql && numbers.Contains(sql.Number);

    private sealed record AttemptedWrite(ProfilePictureInfo Info, byte[] ContentHash, byte[] RowVersion, byte[]? PriorRowVersion);

    private sealed record StoredRow(string ContentType, int Version, byte[] Content, byte[] RowVersion);
}
