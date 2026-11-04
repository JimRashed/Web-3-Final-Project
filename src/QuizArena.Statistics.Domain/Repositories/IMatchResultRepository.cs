using QuizArena.Statistics.Domain.Entities;
using QuizArena.Statistics.Domain.Enums;

namespace QuizArena.Statistics.Domain.Repositories;

/// <summary>
/// Persistence port for <see cref="MatchResult"/>, implemented by QuizArena.Statistics.Data.
/// All queries are scoped to one issuer; ordering and tie-breaks match <c>StatisticsCalculator</c>.
/// </summary>
public interface IMatchResultRepository
{
    /// <summary>Read-only lookup by the full (issuer, matchId, userId) key.</summary>
    Task<MatchResult?> FindAsync(string issuer, string matchId, string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a delivered result idempotently: returns <see cref="ResultReplayDecision.New"/> when inserted,
    /// <see cref="ResultReplayDecision.IdempotentReplay"/> when an identical result already exists, and throws
    /// <c>ResultConflictException</c> when a different result exists. Safe under concurrent deliveries.
    /// </summary>
    Task<ResultReplayDecision> RecordAsync(MatchResult result, CancellationToken cancellationToken);

    /// <summary>Totals for one player; all zeros when the player has no results.</summary>
    Task<PlayerTotals> GetTotalsAsync(string issuer, string userId, CancellationToken cancellationToken);

    /// <summary>One page of a player's history, newest first, then match id (ordinal).</summary>
    Task<IReadOnlyList<MatchResult>> GetHistoryAsync(string issuer, string userId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Top players by total score, then wins, then user id (ordinal).</summary>
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(string issuer, int top, CancellationToken cancellationToken);
}
