namespace QuizArena.Notification.Api.Security;

/// <summary>Named policies from docs/milestone3/authorization-matrix.md; each name equals its scope.</summary>
public static class NotificationPolicies
{
    public const string EventsRead = "events.read";
    public const string PreferencesRead = "preferences.read";
    public const string PreferencesWrite = "preferences.write";
    public const string EventsWrite = "events.write";

    public static IReadOnlyDictionary<string, ActorKind> All { get; } = new Dictionary<string, ActorKind>(StringComparer.Ordinal)
    {
        [EventsRead] = ActorKind.User,
        [PreferencesRead] = ActorKind.User,
        [PreferencesWrite] = ActorKind.User,
        [EventsWrite] = ActorKind.Machine,
    };
}
