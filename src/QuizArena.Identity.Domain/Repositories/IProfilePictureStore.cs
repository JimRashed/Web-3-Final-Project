using QuizArena.Identity.Domain.Entities;

namespace QuizArena.Identity.Domain.Repositories;

/// <summary>
/// Private persistence port for a profile's single picture, implemented by QuizArena.Identity.Data.
/// The profile id must come from the validated token's exact issuer + subject; callers never supply keys, paths or URLs.
/// </summary>
public interface IProfilePictureStore
{
    /// <summary>The stored picture reference without its bytes, or null when the profile has no picture.</summary>
    Task<ProfilePictureInfo?> GetInfoAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>The stored picture including its bytes, or null when the profile has no picture.</summary>
    Task<ProfilePicture?> GetAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically creates or replaces the profile's picture. Readers see the previous picture or the new one, never a
    /// partial write. Throws <see cref="Exceptions.ProfilePictureConflictException"/> when a concurrent replace
    /// committed first and <see cref="Exceptions.PlayerProfileNotFoundException"/> when the profile does not exist.
    /// Throws <see cref="Exceptions.ProfilePictureCommitOutcomeUnknownException"/> when the commit step failed and the
    /// outcome could not be verified; a retry may then add one more version.
    /// </summary>
    Task<ProfilePictureInfo> ReplaceAsync(Guid profileId, string contentType, byte[] content, DateTime updatedAt, CancellationToken cancellationToken);

    /// <summary>Removes the profile's picture. Returns false when there was none.</summary>
    Task<bool> DeleteAsync(Guid profileId, CancellationToken cancellationToken);
}
