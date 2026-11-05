using QuizArena.Statistics.Domain.Exceptions;

namespace QuizArena.Statistics.Domain.Entities;

/// <summary>
/// One player's immutable outcome for one finished match, delivered by the Gameplay service.
/// The key is (Issuer, MatchId, UserId). A draw is recorded as <see cref="Won"/> = false for both players.
/// </summary>
public sealed class MatchResult
{
    public const int MaxIssuerLength = 256;
    public const int MaxMatchIdLength = 100;
    public const int MaxUserIdLength = 200;

    /// <summary>Upper bound for newly recorded results: five rounds, one point each.</summary>
    public const int MaxNewScore = 5;

    private MatchResult(string issuer, string matchId, string userId, int score, bool won, DateTime playedAt)
    {
        Issuer = issuer;
        MatchId = matchId;
        UserId = userId;
        Score = score;
        Won = won;
        PlayedAt = playedAt;
    }

    /// <summary>Configured trusted issuer the player subject belongs to.</summary>
    public string Issuer { get; }

    /// <summary>Opaque match identifier; new matches use UUID text, legacy values are kept verbatim.</summary>
    public string MatchId { get; }

    /// <summary>Opaque, case-sensitive player subject.</summary>
    public string UserId { get; }

    public int Score { get; }

    /// <summary>True only for the sole higher scorer; false for a loss or a draw.</summary>
    public bool Won { get; }

    /// <summary>Original match completion time (UTC), not the delivery time.</summary>
    public DateTime PlayedAt { get; }

    /// <summary>Records a new result from Gameplay. Score must be within 0..<see cref="MaxNewScore"/>.</summary>
    public static MatchResult Record(string issuer, string matchId, string userId, int score, bool won, DateTime playedAt)
    {
        if (score is < 0 or > MaxNewScore)
        {
            throw new InvalidMatchResultException(nameof(score), $"Score must be between 0 and {MaxNewScore}.");
        }

        return Build(issuer, matchId, userId, score, won, playedAt);
    }

    /// <summary>
    /// Rehydrates a stored or imported historical result. Legacy matches may have scores above
    /// <see cref="MaxNewScore"/>, so only non-negativity is enforced.
    /// </summary>
    public static MatchResult Rehydrate(string issuer, string matchId, string userId, int score, bool won, DateTime playedAt)
    {
        if (score < 0)
        {
            throw new InvalidMatchResultException(nameof(score), "Score must not be negative.");
        }

        return Build(issuer, matchId, userId, score, won, playedAt);
    }

    /// <summary>True when both results share the (Issuer, MatchId, UserId) key, compared ordinally.</summary>
    public bool HasSameKeyAs(MatchResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Issuer, other.Issuer, StringComparison.Ordinal)
            && string.Equals(MatchId, other.MatchId, StringComparison.Ordinal)
            && string.Equals(UserId, other.UserId, StringComparison.Ordinal);
    }

    /// <summary>True when the key and every recorded value (score, outcome, played time) are equal.</summary>
    public bool IsSameResultAs(MatchResult other) =>
        HasSameKeyAs(other) && Score == other.Score && Won == other.Won && PlayedAt == other.PlayedAt;

    private static MatchResult Build(string issuer, string matchId, string userId, int score, bool won, DateTime playedAt)
    {
        if (playedAt.Kind != DateTimeKind.Utc || playedAt == default)
        {
            throw new InvalidMatchResultException(nameof(playedAt), "PlayedAt must be a UTC timestamp.");
        }

        return new MatchResult(
            ValidateIdentifier(issuer, nameof(issuer), MaxIssuerLength),
            ValidateIdentifier(matchId, nameof(matchId), MaxMatchIdLength),
            ValidateIdentifier(userId, nameof(userId), MaxUserIdLength),
            score,
            won,
            playedAt);
    }

    // Identifiers are opaque and compared exactly, so padded values are rejected rather than trimmed.
    private static string ValidateIdentifier(string value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidMatchResultException(field, $"{field} is required.");
        }

        if (value.Length > maxLength)
        {
            throw new InvalidMatchResultException(field, $"{field} must be at most {maxLength} characters.");
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidMatchResultException(field, $"{field} must not have leading or trailing whitespace.");
        }

        return value;
    }
}
