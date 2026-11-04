using QuizArena.Notification.Domain.Exceptions;

namespace QuizArena.Notification.Domain.Entities;

/// <summary>
/// A player's notification channel preferences, keyed by issuer and opaque subject.
/// A player without a stored row is treated as opted out of both channels, matching the contract default.
/// </summary>
public sealed class UserPreference
{
    public const int MaxIssuerLength = 256;
    public const int MaxSubjectLength = 200;

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

    /// <summary>Preferences used when the player has never saved any: both channels disabled.</summary>
    public static UserPreference CreateDefault(string issuer, string subject) =>
        new(ValidateOpaque(issuer, "issuer", MaxIssuerLength), ValidateOpaque(subject, "subject", MaxSubjectLength), false, false);

    /// <summary>Rehydrates a stored preference row without re-applying defaults.</summary>
    public static UserPreference Rehydrate(string issuer, string subject, bool emailEnabled, bool pushEnabled) =>
        new(
            ValidateOpaque(issuer, "issuer", MaxIssuerLength),
            ValidateOpaque(subject, "subject", MaxSubjectLength),
            emailEnabled,
            pushEnabled);

    public void Update(bool emailEnabled, bool pushEnabled)
    {
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;
    }

    // The subject is opaque and compared exactly, so padded values are rejected rather than trimmed.
    private static string ValidateOpaque(string value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NotificationDomainException($"Preference {field} is required.");
        }

        if (value.Length > maxLength)
        {
            throw new NotificationDomainException($"Preference {field} must be at most {maxLength} characters.");
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new NotificationDomainException($"Preference {field} must not have leading or trailing whitespace.");
        }

        return value;
    }
}
