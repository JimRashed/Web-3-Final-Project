namespace QuizArena.Statistics.Api.Security;

/// <summary>Named policies from docs/milestone3/authorization-matrix.md; each name equals its scope.</summary>
public static class StatisticsPolicies
{
    public const string StatsRead = "stats.read";
    public const string LeaderboardRead = "leaderboard.read";
    public const string ResultsWrite = "results.write";

    public static IReadOnlyDictionary<string, ActorKind> All { get; } = new Dictionary<string, ActorKind>(StringComparer.Ordinal)
    {
        [StatsRead] = ActorKind.User,
        [LeaderboardRead] = ActorKind.User,
        [ResultsWrite] = ActorKind.Machine,
    };
}
