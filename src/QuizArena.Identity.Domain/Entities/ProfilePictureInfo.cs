namespace QuizArena.Identity.Domain.Entities;

/// <summary>
/// The approved, private reference to a player's stored picture (SEC-03): content type, version and last update.
/// It deliberately carries no storage key, path, URL or bytes.
/// </summary>
public sealed record ProfilePictureInfo(string ContentType, int Version, DateTime UpdatedAt);
