using QuizArena.Gameplay.Domain.Entities;

namespace QuizArena.Gameplay.Domain.Repositories;

/// <summary>
/// Persistence port for the <see cref="Match"/> aggregate, implemented by QuizArena.Gameplay.Data.
/// A match is always loaded and saved as a whole.
/// </summary>
public interface IMatchRepository
{
    /// <summary>Read-only load by id; returns null when the match is unknown.</summary>
    Task<Match?> FindAsync(string matchId, CancellationToken cancellationToken);

    /// <summary>Inserts a new match or replaces the stored state of an existing one.</summary>
    Task SaveAsync(Match match, CancellationToken cancellationToken);
}
