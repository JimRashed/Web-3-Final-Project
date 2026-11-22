using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
/// Identity does not enable <c>EnableRetryOnFailure</c>. If it ever does, the explicit transaction in
/// <see cref="ReplaceAsync"/> must move inside <c>Database.CreateExecutionStrategy().ExecuteAsync</c>.
/// </remarks>
public sealed class ProfilePictureStore(IdentityDbContext context) : IProfilePictureStore
{
    // SQL Server error numbers: duplicate key (unique index / primary key) and foreign-key violation.
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;
    private const int ForeignKeyViolation = 547;

    public Task<ProfilePictureInfo?> GetInfoAsync(Guid profileId, CancellationToken cancellationToken) =>
        context.Set<ProfilePicture>()
            .AsNoTracking()
            .Where(picture => picture.ProfileId == profileId)
            .Select(picture => new ProfilePictureInfo(picture.ContentType, picture.Version, picture.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ProfilePicture?> GetAsync(Guid profileId, CancellationToken cancellationToken) =>
        context.Set<ProfilePicture>()
            .AsNoTracking()
            .SingleOrDefaultAsync(picture => picture.ProfileId == profileId, cancellationToken);

    public async Task<ProfilePictureInfo> ReplaceAsync(
        Guid profileId,
        string contentType,
        byte[] content,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        var succeeded = false;
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var picture = await context.Set<ProfilePicture>()
                .SingleOrDefaultAsync(p => p.ProfileId == profileId, cancellationToken);

            // Domain validation runs before any SQL write.
            if (picture is null)
            {
                picture = ProfilePicture.Create(profileId, contentType, content, updatedAt);
                context.Add(picture);
            }
            else
            {
                picture.Replace(contentType, content, updatedAt);
            }

            // The UPDATE carries WHERE RowVersion = @original; a concurrent committed replace makes it affect 0 rows.
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            succeeded = true;
            return picture.ToInfo();
        }
        catch (DbUpdateConcurrencyException exception)
        {
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
            if (!succeeded)
            {
                // The uncommitted transaction was rolled back on dispose; start the next attempt in this scope clean.
                context.ChangeTracker.Clear();
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

    private static bool HasSqlError(DbUpdateException exception, params int[] numbers) =>
        exception.InnerException is SqlException sql && numbers.Contains(sql.Number);
}
