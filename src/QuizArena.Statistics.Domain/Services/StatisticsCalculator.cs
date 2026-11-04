using QuizArena.Statistics.Domain.Entities;
using QuizArena.Statistics.Domain.Enums;
using QuizArena.Statistics.Domain.Exceptions;

namespace QuizArena.Statistics.Domain.Services;

/// <summary>
/// Pure, in-memory Statistics rules: totals, leaderboard ranking, history ordering and replay decisions.
/// Aggregation groups by <see cref="MatchResult.UserId"/> alone because each deployment trusts exactly one
/// issuer (ADR-05); callers pass results for the configured issuer only.
/// </summary>
public static class StatisticsCalculator
{
    public const int MaxLeaderboardSize = 100;
    public const int DefaultLeaderboardSize = 10;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    /// <summary>History paging rules: page is 1-based; page size is 1..<see cref="MaxPageSize"/>.</summary>
    public static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new InvalidMatchResultException(nameof(page), "Page must be 1 or greater.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            throw new InvalidMatchResultException(nameof(pageSize), $"Page size must be between 1 and {MaxPageSize}.");
        }
    }

    /// <summary>Leaderboard size rule: 1..<see cref="MaxLeaderboardSize"/>.</summary>
    public static void ValidateTop(int top)
    {
        if (top is < 1 or > MaxLeaderboardSize)
        {
            throw new InvalidMatchResultException(nameof(top), $"Top must be between 1 and {MaxLeaderboardSize}.");
        }
    }

    /// <summary>
    /// Totals for one player. Every result counts as a game; only <see cref="MatchResult.Won"/> counts as a win,
    /// so draws add a game but not a win.
    /// </summary>
    public static PlayerTotals Summarize(string userId, IEnumerable<MatchResult> results)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(results);

        var games = 0;
        var wins = 0;
        var total = 0;
        foreach (var result in results.Where(r => string.Equals(r.UserId, userId, StringComparison.Ordinal)))
        {
            games++;
            wins += result.Won ? 1 : 0;
            total = checked(total + result.Score);
        }

        return new PlayerTotals(userId, games, wins, total);
    }

    /// <summary>
    /// Top players ordered by total score descending, then wins descending, then user id (ordinal ascending)
    /// so ties always resolve the same way.
    /// </summary>
    public static IReadOnlyList<LeaderboardEntry> BuildLeaderboard(IEnumerable<MatchResult> results, int top = DefaultLeaderboardSize)
    {
        ArgumentNullException.ThrowIfNull(results);
        ValidateTop(top);

        return results
            .GroupBy(r => r.UserId, StringComparer.Ordinal)
            .Select(group => Summarize(group.Key, group))
            .OrderByDescending(t => t.TotalScore)
            .ThenByDescending(t => t.Wins)
            .ThenBy(t => t.UserId, StringComparer.Ordinal)
            .Take(top)
            .Select((t, index) => new LeaderboardEntry(index + 1, t.UserId, t.TotalScore, t.Wins, t.GamesPlayed))
            .ToList();
    }

    /// <summary>Match history newest first; results played at the same instant are ordered by match id (ordinal).</summary>
    public static IReadOnlyList<MatchResult> OrderHistory(IEnumerable<MatchResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results
            .OrderByDescending(r => r.PlayedAt)
            .ThenBy(r => r.MatchId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Decides how to handle a delivery for a key that may already be stored. Identical content is an idempotent
    /// replay; any difference in score, outcome or played time is a conflict.
    /// </summary>
    public static ResultReplayDecision DecideReplay(MatchResult? existing, MatchResult incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (existing is null)
        {
            return ResultReplayDecision.New;
        }

        if (!existing.HasSameKeyAs(incoming))
        {
            throw new ArgumentException("The stored result must have the same key as the incoming result.", nameof(existing));
        }

        return existing.IsSameResultAs(incoming) ? ResultReplayDecision.IdempotentReplay : ResultReplayDecision.Conflict;
    }

    /// <summary>Like <see cref="DecideReplay"/> but throws <see cref="ResultConflictException"/> on conflict.</summary>
    public static bool ShouldPersist(MatchResult? existing, MatchResult incoming) =>
        DecideReplay(existing, incoming) switch
        {
            ResultReplayDecision.New => true,
            ResultReplayDecision.IdempotentReplay => false,
            _ => throw new ResultConflictException(incoming.MatchId, incoming.UserId),
        };
}
