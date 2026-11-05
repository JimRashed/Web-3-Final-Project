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

    /// <summary>
    /// The match created for a lobby, if any. A lobby starts exactly one match, so a repeated start
    /// returns the existing match instead of creating a second one.
    /// </summary>
    Task<Match?> FindByLobbyAsync(string lobbyId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or replaces the match and, in the same transaction, enqueues the outbound deliveries.
    /// The two must commit together: a stored match without its creation deliveries (or a delivery
    /// for a match that was rolled back) is never valid.
    /// </summary>
    Task SaveAsync(Match match, IReadOnlyList<MatchDelivery> deliveries, CancellationToken cancellationToken);

    /// <summary>Saves the match without enqueueing deliveries (reads and mid-match answer updates).</summary>
    Task SaveAsync(Match match, CancellationToken cancellationToken) =>
        SaveAsync(match, [], cancellationToken);
}
