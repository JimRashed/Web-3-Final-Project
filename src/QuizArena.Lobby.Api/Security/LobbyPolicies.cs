namespace QuizArena.Lobby.Api.Security;

/// <summary>Named policies from docs/milestone3/authorization-matrix.md; each name equals its scope.</summary>
public static class LobbyPolicies
{
    public const string LobbiesRead = "lobbies.read";
    public const string LobbiesWrite = "lobbies.write";
    public const string LobbiesVerify = "lobbies.verify";

    public static IReadOnlyDictionary<string, ActorKind> All { get; } = new Dictionary<string, ActorKind>(StringComparer.Ordinal)
    {
        [LobbiesRead] = ActorKind.User,
        [LobbiesWrite] = ActorKind.User,
        [LobbiesVerify] = ActorKind.Machine,
    };
}
