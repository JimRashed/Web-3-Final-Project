using QuizArena.Statistics.Api.Contracts;
using QuizArena.Statistics.Api.Security;
using QuizArena.Statistics.Domain.Entities;
using QuizArena.Statistics.Domain.Enums;
using QuizArena.Statistics.Domain.Exceptions;
using QuizArena.Statistics.Domain.Repositories;
using QuizArena.Statistics.Domain.Services;

namespace QuizArena.Statistics.Api.Services;

public sealed class StatisticsApplication(IMatchResultRepository results) : IStatisticsApplication
{
    public Task<ResultReplayDecision> RecordResultAsync(AuthenticatedActor machine, RecordResultRequest request, CancellationToken cancellationToken)
    {
        EnsureKind(machine, ActorKind.Machine);
        ArgumentNullException.ThrowIfNull(request);

        // The player subject belongs to the same (single, configured) issuer that signed the Gameplay token.
        var result = MatchResult.Record(
            machine.Issuer,
            request.MatchId ?? throw Missing("matchId"),
            request.UserId ?? throw Missing("userId"),
            request.Score ?? throw Missing("score"),
            request.Won ?? throw Missing("won"),
            (request.PlayedAt ?? throw Missing("playedAt")).UtcDateTime);

        return results.RecordAsync(result, cancellationToken);
    }

    public async Task<StatsResponse> GetStatsAsync(AuthenticatedActor actor, string userId, CancellationToken cancellationToken)
    {
        EnsureSelf(actor, userId);
        var totals = await results.GetTotalsAsync(actor.Issuer, actor.Subject, cancellationToken);
        return StatsResponse.From(totals);
    }

    public async Task<IReadOnlyList<HistoryItemResponse>> GetHistoryAsync(
        AuthenticatedActor actor,
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // Contract error order: input validation (400) before ownership (403).
        StatisticsCalculator.ValidatePaging(page, pageSize);
        EnsureSelf(actor, userId);
        var history = await results.GetHistoryAsync(actor.Issuer, actor.Subject, page, pageSize, cancellationToken);
        return history.Select(HistoryItemResponse.From).ToList();
    }

    public async Task<IReadOnlyList<LeaderResponse>> GetLeaderboardAsync(AuthenticatedActor actor, int top, CancellationToken cancellationToken)
    {
        StatisticsCalculator.ValidateTop(top);
        EnsureKind(actor, ActorKind.User);
        var board = await results.GetLeaderboardAsync(actor.Issuer, top, cancellationToken);
        return board.Select(LeaderResponse.From).ToList();
    }

    private static void EnsureSelf(AuthenticatedActor actor, string userId)
    {
        EnsureKind(actor, ActorKind.User);
        if (!string.Equals(actor.Subject, userId, StringComparison.Ordinal))
        {
            throw new ForbiddenAccessException();
        }
    }

    private static void EnsureKind(AuthenticatedActor actor, ActorKind kind)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != kind)
        {
            throw new InvalidOperationException($"This operation requires a {kind.ToString().ToLowerInvariant()} actor.");
        }
    }

    private static InvalidMatchResultException Missing(string field) => new(field, $"{field} is required.");
}
