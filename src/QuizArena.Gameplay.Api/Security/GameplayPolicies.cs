namespace QuizArena.Gameplay.Api.Security;

/// <summary>Named policies from docs/milestone3/authorization-matrix.md; each name equals its scope.</summary>
public static class GameplayPolicies
{
    public const string MatchesRead = "matches.read";
    public const string MatchesWrite = "matches.write";

    public static IReadOnlyDictionary<string, ActorKind> All { get; } = new Dictionary<string, ActorKind>(StringComparer.Ordinal)
    {
        [MatchesRead] = ActorKind.User,
        [MatchesWrite] = ActorKind.User,
    };
}
