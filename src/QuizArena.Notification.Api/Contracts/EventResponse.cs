using System.Text.Json.Serialization;
using QuizArena.Notification.Domain.Entities;
using QuizArena.Notification.Domain.Enums;

namespace QuizArena.Notification.Api.Contracts;

/// <summary>A stored notification event as returned by the API.</summary>
public sealed record EventResponse(
    Guid EventId,
    string Type,
    string UserId,
    EventPayloadResponse Payload,
    DateTime CreatedAt)
{
    public static EventResponse FromDomain(Event @event) =>
        new(
            @event.EventId,
            @event.Type == EventType.MatchCreated ? "MATCH_CREATED" : "MATCH_COMPLETED",
            @event.UserId,
            new EventPayloadResponse(
                @event.MatchId,
                @event.Type == EventType.MatchCompleted ? @event.WinnerUserId : null,
                @event.Type == EventType.MatchCompleted ? @event.Draw : null),
            @event.CreatedAt);
}

/// <summary>
/// Typed payload on the wire. Null members are omitted, so a <c>MATCH_CREATED</c> event serialises
/// to <c>{ "matchId": "…" }</c> and nothing else.
/// </summary>
public sealed record EventPayloadResponse(
    string MatchId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WinnerUserId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Draw = null);
