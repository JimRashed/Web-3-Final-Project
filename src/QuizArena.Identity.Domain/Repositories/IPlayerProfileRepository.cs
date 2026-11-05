using QuizArena.Identity.Domain.Entities;

namespace QuizArena.Identity.Domain.Repositories;

/// <summary>
/// Persistence port for <see cref="PlayerProfile"/>, implemented by QuizArena.Identity.Data.
/// Profiles are looked up by the exact (issuer, subject) pair from validated token claims.
/// </summary>
public interface IPlayerProfileRepository
{
    /// <summary>Read-only lookup; returns null when the player has no profile yet.</summary>
    Task<PlayerProfile?> FindByIdentityAsync(string issuer, string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Stores <paramref name="candidate"/> unless a profile for the same identity already exists, and returns the
    /// stored profile either way. Safe under concurrent first requests: exactly one row is ever created.
    /// </summary>
    Task<PlayerProfile> AddIfAbsentAsync(PlayerProfile candidate, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the profile, applies <see cref="PlayerProfile.Rename"/> and saves. Returns null when no profile exists.
    /// </summary>
    Task<PlayerProfile?> RenameAsync(string issuer, string subject, string displayName, DateTime updatedAt, CancellationToken cancellationToken);
}
