namespace QuizArena.Statistics.Api.Contracts;

/// <summary>
/// Body of POST /api/results (schema RecordResult), sent only by the Gameplay machine client.
/// Fields are nullable so a missing value becomes a field-level 400 instead of a silent default.
/// </summary>
public sealed record RecordResultRequest(string? MatchId, string? UserId, int? Score, bool? Won, DateTimeOffset? PlayedAt);
