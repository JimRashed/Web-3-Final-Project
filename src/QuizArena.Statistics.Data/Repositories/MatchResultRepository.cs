using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Statistics.Data.Context;
using QuizArena.Statistics.Domain.Entities;
using QuizArena.Statistics.Domain.Enums;
using QuizArena.Statistics.Domain.Repositories;
using QuizArena.Statistics.Domain.Services;

namespace QuizArena.Statistics.Data.Repositories;

public sealed class MatchResultRepository(StatisticsDbContext context) : IMatchResultRepository
{
    // SQL Server duplicate-key error numbers (unique index / primary key).
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;

    private IQueryable<MatchResult> Results => context.Set<MatchResult>().AsNoTracking();

    public Task<MatchResult?> FindAsync(string issuer, string matchId, string userId, CancellationToken cancellationToken) =>
        Results.SingleOrDefaultAsync(
            r => r.Issuer == issuer && r.MatchId == matchId && r.UserId == userId,
            cancellationToken);

    public async Task<ResultReplayDecision> RecordAsync(MatchResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        var existing = await FindAsync(result.Issuer, result.MatchId, result.UserId, cancellationToken);
        if (!StatisticsCalculator.ShouldPersist(existing, result))
        {
            return ResultReplayDecision.IdempotentReplay;
        }

        context.Add(result);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ResultReplayDecision.New;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // A concurrent delivery stored this key first: identical content is a replay, different content conflicts.
            context.Entry(result).State = EntityState.Detached;
            var winner = await FindAsync(result.Issuer, result.MatchId, result.UserId, cancellationToken)
                ?? throw new InvalidOperationException("Result insert conflicted but no stored result was found.", exception);
            // Called only for its check: throws ResultConflictException when the stored content differs.
            _ = StatisticsCalculator.ShouldPersist(winner, result);
            return ResultReplayDecision.IdempotentReplay;
        }
    }

    public async Task<PlayerTotals> GetTotalsAsync(string issuer, string userId, CancellationToken cancellationToken)
    {
        var totals = await Results
            .Where(r => r.Issuer == issuer && r.UserId == userId)
            .GroupBy(r => r.UserId)
            .Select(g => new { Games = g.Count(), Wins = g.Count(r => r.Won), Total = g.Sum(r => r.Score) })
            .SingleOrDefaultAsync(cancellationToken);

        return totals is null
            ? new PlayerTotals(userId, 0, 0, 0)
            : new PlayerTotals(userId, totals.Games, totals.Wins, totals.Total);
    }

    public async Task<IReadOnlyList<MatchResult>> GetHistoryAsync(
        string issuer,
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        StatisticsCalculator.ValidatePaging(page, pageSize);

        return await Results
            .Where(r => r.Issuer == issuer && r.UserId == userId)
            .OrderByDescending(r => r.PlayedAt)
            .ThenBy(r => r.MatchId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(string issuer, int top, CancellationToken cancellationToken)
    {
        StatisticsCalculator.ValidateTop(top);

        var rows = await Results
            .Where(r => r.Issuer == issuer)
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(r => r.Score), Wins = g.Count(r => r.Won), Games = g.Count() })
            .OrderByDescending(row => row.Total)
            .ThenByDescending(row => row.Wins)
            .ThenBy(row => row.UserId)
            .Take(top)
            .ToListAsync(cancellationToken);

        return rows.Select((row, index) => new LeaderboardEntry(index + 1, row.UserId, row.Total, row.Wins, row.Games)).ToList();
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or PrimaryKeyViolation };
}
