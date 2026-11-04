using QuizArena.Statistics.Api.Contracts;
using QuizArena.Statistics.Api.Security;
using QuizArena.Statistics.Domain.Enums;

namespace QuizArena.Statistics.Api.Services;

/// <summary>Statistics application operations (contract name IStatisticsApplication).</summary>
public interface IStatisticsApplication
{
    /// <summary>Records a result delivered by the Gameplay machine client; identical retries are no-ops.</summary>
    Task<ResultReplayDecision> RecordResultAsync(AuthenticatedActor machine, RecordResultRequest request, CancellationToken cancellationToken);

    /// <summary>The caller's own totals; <paramref name="userId"/> must be the caller's subject.</summary>
    Task<StatsResponse> GetStatsAsync(AuthenticatedActor actor, string userId, CancellationToken cancellationToken);

    /// <summary>One page of the caller's own history, newest first.</summary>
    Task<IReadOnlyList<HistoryItemResponse>> GetHistoryAsync(AuthenticatedActor actor, string userId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Leaderboard visible to any signed-in player.</summary>
    Task<IReadOnlyList<LeaderResponse>> GetLeaderboardAsync(AuthenticatedActor actor, int top, CancellationToken cancellationToken);
}
