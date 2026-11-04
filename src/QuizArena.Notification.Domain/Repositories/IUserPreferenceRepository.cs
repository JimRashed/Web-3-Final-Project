using QuizArena.Notification.Domain.Entities;

namespace QuizArena.Notification.Domain.Repositories;

/// <summary>
/// Persistence port for <see cref="UserPreference"/>, implemented by QuizArena.Notification.Data.
/// </summary>
public interface IUserPreferenceRepository
{
    /// <summary>Read-only lookup by exact identity; returns null when the player never saved preferences.</summary>
    Task<UserPreference?> FindAsync(string issuer, string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or updates the stored preferences for the identity and returns the stored row.
    /// </summary>
    Task<UserPreference> UpsertAsync(UserPreference preference, CancellationToken cancellationToken);
}
