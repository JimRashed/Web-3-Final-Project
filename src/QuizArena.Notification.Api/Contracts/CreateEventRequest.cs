using System.Text.Json.Serialization;

namespace QuizArena.Notification.Api.Contracts;

/// <summary>
/// Body of <c>POST /events</c>. Extra members are rejected so a producer cannot smuggle
/// fields the typed payload does not define.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateEventRequest(
    Guid EventId,
    string Type,
    string UserId,
    CreateEventPayload Payload);

/// <summary>
/// Typed match payload. <c>MATCH_CREATED</c> carries only <see cref="MatchId"/>;
/// <c>MATCH_COMPLETED</c> additionally carries <see cref="WinnerUserId"/> and <see cref="Draw"/>.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateEventPayload(
    string? MatchId,
    string? WinnerUserId = null,
    bool? Draw = null);
