namespace QuizArena.Identity.Api.Contracts;

/// <summary>
/// Body of PUT /api/users/me (schema UpdateProfile). It deliberately has no user id: the profile owner always
/// comes from the validated token.
/// </summary>
public sealed record UpdateProfileRequest(string? DisplayName);
