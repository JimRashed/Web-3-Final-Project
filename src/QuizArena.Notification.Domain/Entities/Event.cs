using System.Text.Json;
using QuizArena.Notification.Domain.Exceptions;

namespace QuizArena.Notification.Domain.Entities;

/// <summary>
/// An immutable notification event owned by a single player.
/// Events are append-only: once stored, neither the payload nor the creation time changes.
/// </summary>
public sealed class Event
{
    public const int MaxTypeLength = 64;
    public const int MaxUserIdLength = 256;
    public const int MaxPayloadLength = 16 * 1024;

    private Event(Guid eventId, string type, string userId, string payloadJson, DateTime createdAt)
    {
        EventId = eventId;
        Type = type;
        UserId = userId;
        PayloadJson = payloadJson;
        CreatedAt = createdAt;
    }

    /// <summary>Producer-supplied identifier, unique within the issuer.</summary>
    public Guid EventId { get; }

    /// <summary>Event discriminator such as <c>MATCH_COMPLETED</c>.</summary>
    public string Type { get; }

    /// <summary>Opaque subject taken from the validated token; never a request-body value.</summary>
    public string UserId { get; }

    /// <summary>Canonical JSON object payload. Immutable once the event is stored.</summary>
    public string PayloadJson { get; }

    /// <summary>UTC instant at which the event was accepted.</summary>
    public DateTime CreatedAt { get; }

    public static Event Create(
        string type,
        string userId,
        string payloadJson,
        DateTime createdAt,
        Guid? eventId = null)
    {
        var normalizedType = ValidateType(type);
        var normalizedUser = ValidateUser(userId);
        var canonicalPayload = NormalizePayload(payloadJson);
        EnsureUtc(createdAt);

        return new Event(eventId ?? Guid.NewGuid(), normalizedType, normalizedUser, canonicalPayload, createdAt);
    }

    /// <summary>
    /// True when both events carry the same canonical payload, i.e. the replay is idempotent.
    /// </summary>
    public bool HasSamePayload(Event? other) =>
        other is not null && string.Equals(PayloadJson, other.PayloadJson, StringComparison.Ordinal);

    private static string ValidateType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new InvalidEventException("Event type is required.");
        }

        var trimmed = type.Trim();
        if (trimmed.Length > MaxTypeLength)
        {
            throw new InvalidEventException($"Event type must be at most {MaxTypeLength} characters.");
        }

        foreach (var character in trimmed)
        {
            var allowed = char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character == '_';
            if (!allowed)
            {
                throw new InvalidEventException(
                    $"Event type '{trimmed}' must use upper-case letters, digits or underscores.");
            }
        }

        return trimmed;
    }

    private static string ValidateUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidEventException("Event user is required.");
        }

        var trimmed = userId.Trim();
        if (trimmed.Length > MaxUserIdLength)
        {
            throw new InvalidEventException($"Event user must be at most {MaxUserIdLength} characters.");
        }

        return trimmed;
    }

    private static string NormalizePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidEventException("Event payload is required.");
        }

        if (payloadJson.Length > MaxPayloadLength)
        {
            throw new InvalidEventException($"Event payload must be at most {MaxPayloadLength} characters.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException)
        {
            throw new InvalidEventException("Event payload must be valid JSON.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidEventException("Event payload must be a JSON object.");
            }

            // Canonicalise so that replays compare on content, not on formatting or whitespace.
            return JsonSerializer.Serialize(document.RootElement);
        }
    }

    private static void EnsureUtc(DateTime createdAt)
    {
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new InvalidEventException("Event creation time must be expressed in UTC.");
        }
    }
}
