using QuizArena.Identity.Domain.Exceptions;

namespace QuizArena.Identity.Domain.Entities;

/// <summary>
/// A player's QuizArena profile, keyed by the trusted OIDC issuer and opaque subject.
/// Sign-in, registration and credentials belong to the identity provider; no password or token data lives here.
/// </summary>
public sealed class PlayerProfile
{
    public const int MaxIssuerLength = 256;
    public const int MaxSubjectLength = 200;
    public const int MaxDisplayNameLength = 80;
    public const string FallbackDisplayName = "Player";

    private PlayerProfile(Guid id, string issuer, string subject, string displayName, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        Issuer = issuer;
        Subject = subject;
        DisplayName = displayName;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>Internal identifier; stable across display-name changes.</summary>
    public Guid Id { get; }

    /// <summary>Exact issuer of the validated token (e.g. the Keycloak realm URL).</summary>
    public string Issuer { get; }

    /// <summary>Opaque, case-sensitive subject from the validated token. Never parsed, trimmed or lowercased.</summary>
    public string Subject { get; }

    public string DisplayName { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Creates a profile for an identity taken from validated token claims.</summary>
    public static PlayerProfile Create(string issuer, string subject, string displayName, DateTime createdAt, Guid? id = null)
    {
        EnsureUtc(createdAt, nameof(createdAt));
        var profileId = id ?? Guid.NewGuid();
        EnsureId(profileId);

        return new PlayerProfile(
            profileId,
            ValidateIssuer(issuer),
            ValidateSubject(subject),
            ValidateDisplayName(displayName),
            createdAt,
            createdAt);
    }

    /// <summary>Rehydrates a stored profile, re-checking invariants so corrupt rows fail loudly.</summary>
    public static PlayerProfile Rehydrate(Guid id, string issuer, string subject, string displayName, DateTime createdAt, DateTime updatedAt)
    {
        EnsureId(id);
        EnsureUtc(createdAt, nameof(createdAt));
        EnsureUtc(updatedAt, nameof(updatedAt));
        if (updatedAt < createdAt)
        {
            throw new InvalidProfileException(nameof(updatedAt), "Profile updatedAt cannot be earlier than createdAt.");
        }

        return new PlayerProfile(id, ValidateIssuer(issuer), ValidateSubject(subject), ValidateDisplayName(displayName), createdAt, updatedAt);
    }

    /// <summary>
    /// Suggests an initial display name from an untrusted hint such as the provider's <c>preferred_username</c>.
    /// The hint is only a prefill; it is never used as an identity key.
    /// </summary>
    public static string SuggestDisplayName(string? hint)
    {
        var trimmed = hint?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Any(char.IsControl))
        {
            return FallbackDisplayName;
        }

        return trimmed.Length <= MaxDisplayNameLength ? trimmed : trimmed[..MaxDisplayNameLength].TrimEnd();
    }

    /// <summary>
    /// Sets a new display name. Returns false (and leaves <see cref="UpdatedAt"/> unchanged) when the
    /// normalized name is identical, so repeated PUTs are idempotent.
    /// </summary>
    public bool Rename(string displayName, DateTime updatedAt)
    {
        var normalized = ValidateDisplayName(displayName);
        EnsureUtc(updatedAt, nameof(updatedAt));
        if (string.Equals(normalized, DisplayName, StringComparison.Ordinal))
        {
            return false;
        }

        if (updatedAt < CreatedAt)
        {
            throw new InvalidProfileException(nameof(updatedAt), "Profile updatedAt cannot be earlier than createdAt.");
        }

        DisplayName = normalized;
        UpdatedAt = updatedAt;
        return true;
    }

    /// <summary>True when this profile belongs to exactly the given issuer and subject (ordinal, case-sensitive).</summary>
    public bool BelongsTo(string issuer, string subject) =>
        string.Equals(Issuer, issuer, StringComparison.Ordinal) && string.Equals(Subject, subject, StringComparison.Ordinal);

    private static string ValidateIssuer(string issuer)
    {
        var value = ValidateExactIdentifier(issuer, nameof(issuer), MaxIssuerLength);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidProfileException(nameof(issuer), "Profile issuer must be an absolute http(s) URI.");
        }

        return value;
    }

    private static string ValidateSubject(string subject) =>
        ValidateExactIdentifier(subject, nameof(subject), MaxSubjectLength);

    // Identity values are compared exactly, so they are rejected rather than silently trimmed.
    private static string ValidateExactIdentifier(string value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidProfileException(field, $"Profile {field} is required.");
        }

        if (value.Length > maxLength)
        {
            throw new InvalidProfileException(field, $"Profile {field} must be at most {maxLength} characters.");
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidProfileException(field, $"Profile {field} must not have leading or trailing whitespace.");
        }

        return value;
    }

    private static string ValidateDisplayName(string displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidProfileException(nameof(displayName), "Display name is required.");
        }

        if (trimmed.Length > MaxDisplayNameLength)
        {
            throw new InvalidProfileException(nameof(displayName), $"Display name must be at most {MaxDisplayNameLength} characters.");
        }

        if (trimmed.Any(char.IsControl))
        {
            throw new InvalidProfileException(nameof(displayName), "Display name must not contain control characters.");
        }

        return trimmed;
    }

    private static void EnsureId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new InvalidProfileException(nameof(id), "Profile id must not be empty.");
        }
    }

    private static void EnsureUtc(DateTime value, string field)
    {
        if (value.Kind != DateTimeKind.Utc || value == default)
        {
            throw new InvalidProfileException(field, $"Profile {field} must be a UTC timestamp.");
        }
    }
}
