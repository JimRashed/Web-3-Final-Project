using System.Text.Json.Serialization;

namespace QuizArena.Notification.Api.Contracts;

/// <summary>Notification channel preferences; both default to disabled when the player never saved any.</summary>
public sealed record UserPreferencesResponse(bool EmailEnabled, bool PushEnabled);

/// <summary>Body of <c>PUT /users/{userId}/preferences</c>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdatePreferencesRequest(bool EmailEnabled, bool PushEnabled);
