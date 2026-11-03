using QuizArena.Notification.Domain.Exceptions;

namespace QuizArena.Notification.Domain.Entities;

/// <summary>
/// A player's notification channel preferences, keyed by issuer and opaque subject.
/// A player without a stored row is treated as opted in to both channels.
/// </summary>
public sealed class UserPreference
{
    public const int MaxIssuerLength = 256;
    public const int MaxSubjectLength = 256;

    private UserPreference(string issuer, string subject, bool emailEnabled, bool pushEnabled)
    {
        Issuer = issuer;
        Subject = subject;
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;
    }

    public string Issuer { get; }

    public string Subject { get; }

    public bool EmailEnabled { get; private set; }

    public bool PushEnabled { get; private set; }

    /// <summary>Preferences used when the player has never saved any: both channels enabled.</summary>
    public static UserPreference CreateDefault(string issuer, string subject) =>
        new(ValidateIdentity(issuer, nameof(issuer)), ValidateIdentity(subject, nameof(subject)), true, true);

    /// <summary>Rehydrates a stored preference row without re-applying defaults.</summary>
    public static UserPreference Rehydrate(string issuer, string subject, bool emailEnabled, bool pushEnabled) =>
        new(ValidateIdentity(issuer, nameof(issuer)), ValidateIdentity(subject, nameof(subject)), emailEnabled, pushEnabled);

    public void Update(bool emailEnabled, bool pushEnabled)
    {
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;
    }

    private static string ValidateIdentity(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NotificationDomainException($"Preference {name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxSubjectLength)
        {
            throw new NotificationDomainException($"Preference {name} must be at most {MaxSubjectLength} characters.");
        }

        return trimmed;
    }
}
