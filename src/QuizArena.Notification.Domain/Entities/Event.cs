using QuizArena.Notification.Domain.Enums;
using QuizArena.Notification.Domain.Exceptions;

namespace QuizArena.Notification.Domain.Entities;

/// <summary>
/// An immutable notification event delivered by the Gameplay service to one recipient.
/// The payload is typed: <see cref="EventType.MatchCreated"/> carries only the match id and
/// <see cref="EventType.MatchCompleted"/> carries the match outcome.
/// </summary>
public sealed class Event
{
    public const int MaxIssuerLength = 256;
    public const int MaxSubjectLength = 200;
    public const int MaxMatchIdLength = 100;

    private Event(
        Guid eventId,
        string issuer,
        EventType type,
        string userId,
        string matchId,
        string? winnerUserId,
        bool draw,
        DateTime createdAt)
    {
        EventId = eventId;
        Issuer = issuer;
        Type = type;
        UserId = userId;
        MatchId = matchId;
        WinnerUserId = winnerUserId;
        Draw = draw;
        CreatedAt = createdAt;
    }

    /// <summary>Gameplay-generated identifier and idempotency key.</summary>
    public Guid EventId { get; }

    /// <summary>Trusted issuer context the recipient subject belongs to.</summary>
    public string Issuer { get; }

    public EventType Type { get; }

    /// <summary>Recipient subject, taken from the validated machine token only.</summary>
    public string UserId { get; }

    /// <summary>Match the event refers to; the only payload field a created event carries.</summary>
    public string MatchId { get; }

    /// <summary>Winner subject for a completed match; null for a draw or for a created match.</summary>
    public string? WinnerUserId { get; }

    /// <summary>True only for a completed match without a winner.</summary>
    public bool Draw { get; }

    public DateTime CreatedAt { get; }

    public static Event CreateMatchCreated(
        Guid eventId,
        string issuer,
        string recipientSubject,
        string matchId,
        DateTime createdAt)
    {
        var validCreatedAt = ValidateTimestamp(createdAt);

        return new Event(
            ValidateEventId(eventId),
            ValidateOpaque(issuer, "issuer", MaxIssuerLength),
            EventType.MatchCreated,
            ValidateOpaque(recipientSubject, "recipient subject", MaxSubjectLength),
            ValidateOpaque(matchId, "match id", MaxMatchIdLength),
            null,
            false,
            validCreatedAt);
    }

    public static Event CreateMatchCompleted(
        Guid eventId,
        string issuer,
        string recipientSubject,
        string matchId,
        string? winnerUserId,
        bool draw,
        DateTime createdAt)
    {
        if (draw && winnerUserId is not null)
        {
            throw new InvalidEventException("A drawn match must not name a winner.");
        }

        if (!draw && winnerUserId is null)
        {
            throw new InvalidEventException("A completed match without a draw requires a winner.");
        }

        var validCreatedAt = ValidateTimestamp(createdAt);

        return new Event(
            ValidateEventId(eventId),
            ValidateOpaque(issuer, "issuer", MaxIssuerLength),
            EventType.MatchCompleted,
            ValidateOpaque(recipientSubject, "recipient subject", MaxSubjectLength),
            ValidateOpaque(matchId, "match id", MaxMatchIdLength),
            winnerUserId is null ? null : ValidateOpaque(winnerUserId, "winner subject", MaxSubjectLength),
            draw,
            validCreatedAt);
    }

    /// <summary>
    /// Typed equality of the immutable payload: type, recipient, match and outcome.
    /// Member order and whitespace cannot influence the comparison.
    /// </summary>
    public bool HasSameContent(Event? other) =>
        other is not null
        && Type == other.Type
        && string.Equals(UserId, other.UserId, StringComparison.Ordinal)
        && string.Equals(MatchId, other.MatchId, StringComparison.Ordinal)
        && string.Equals(WinnerUserId, other.WinnerUserId, StringComparison.Ordinal)
        && Draw == other.Draw;

    private static Guid ValidateEventId(Guid eventId)
    {
        if (eventId == Guid.Empty)
        {
            throw new InvalidEventException("Event id is required.");
        }

        return eventId;
    }

    // Identifiers and subjects are opaque and compared exactly: padded values are rejected, never trimmed.
    private static string ValidateOpaque(string value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidEventException($"The {field} is required.");
        }

        if (value.Length > maxLength)
        {
            throw new InvalidEventException($"The {field} must be at most {maxLength} characters.");
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidEventException($"The {field} must not have leading or trailing whitespace.");
        }

        return value;
    }

    private static DateTime ValidateTimestamp(DateTime createdAt)
    {
        if (createdAt == default || createdAt.Kind != DateTimeKind.Utc)
        {
            throw new InvalidEventException("Event creation time must be a non-default UTC timestamp.");
        }

        return createdAt;
    }
}
