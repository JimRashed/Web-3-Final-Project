namespace QuizArena.Gameplay.Api.Services;

/// <summary>
/// Addresses of the services Gameplay calls, bound from <c>ServiceUrls</c>. Each URL is the base
/// address of the target API; the typed clients append their route relative to it.
/// </summary>
public sealed class GameplayServiceOptions
{
    public const string SectionName = "ServiceUrls";

    public string Lobby { get; set; } = string.Empty;

    public string QuestionBank { get; set; } = string.Empty;

    public string Statistics { get; set; } = string.Empty;

    public string Notification { get; set; } = string.Empty;

    /// <summary>Per-call timeout for inter-service HTTP (the outbox worker uses the same value).</summary>
    public int RequestTimeoutSeconds { get; set; } = 5;

    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(RequestTimeoutSeconds <= 0 ? 5 : RequestTimeoutSeconds);

    /// <summary>Machine-token scopes; each one is both the policy name and the granted scope.</summary>
    public static class Scopes
    {
        public const string LobbyVerify = "lobbies.verify";
        public const string QuestionsPlay = "questions.play";
        public const string ResultsWrite = "results.write";
        public const string EventsWrite = "events.write";
    }
}
