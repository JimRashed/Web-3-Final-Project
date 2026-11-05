namespace QuizArena.Identity.Api.Security;

/// <summary>Named policies from docs/milestone3/authorization-matrix.md; each name equals its scope.</summary>
public static class IdentityPolicies
{
    public const string ProfilesRead = "profiles.read";
    public const string ProfilesWrite = "profiles.write";

    public static IReadOnlyDictionary<string, ActorKind> All { get; } = new Dictionary<string, ActorKind>(StringComparer.Ordinal)
    {
        [ProfilesRead] = ActorKind.User,
        [ProfilesWrite] = ActorKind.User,
    };
}
