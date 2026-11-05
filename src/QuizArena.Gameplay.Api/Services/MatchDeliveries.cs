using System.Text.Json;
using System.Text.Json.Serialization;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Repositories;

namespace QuizArena.Gameplay.Api.Services;

/// <summary>
/// Builds the outbox rows for a committed match. Payloads and identifiers are frozen here, before the
/// commit, so a retry days later sends exactly what the match produced — not a re-read of state that
/// may have moved on.
/// </summary>
public static class MatchDeliveries
{
    public const string ResultOperation = "results.record.v1";

    public const string EventOperation = "events.publish.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Two <c>MATCH_CREATED</c> events, one per player.</summary>
    public static IReadOnlyList<MatchDelivery> OnCreated(Match match) =>
        match.Players().Select(playerId => Event(match, playerId, "MATCH_CREATED", winnerUserId: null, draw: null)).ToArray();

    /// <summary>
    /// Four rows committed with the final answer: one result per player for Statistics, and one
    /// <c>MATCH_COMPLETED</c> event per player for Notification.
    /// </summary>
    public static IReadOnlyList<MatchDelivery> OnCompleted(Match match)
    {
        if (match.CompletedAt is null)
        {
            throw new InvalidOperationException("A completed match must carry its completion timestamp.");
        }

        var playedAt = new DateTimeOffset(DateTime.SpecifyKind(match.CompletedAt.Value, DateTimeKind.Utc));
        var rows = new List<MatchDelivery>(4);

        foreach (var playerId in match.Players())
        {
            var score = string.Equals(playerId, match.PlayerAId, StringComparison.Ordinal) ? match.PlayerAScore : match.PlayerBScore;
            rows.Add(new MatchDelivery(
                MatchDelivery.Statistics,
                ResultOperation,
                JsonSerializer.Serialize(new
                {
                    matchId = match.MatchId,
                    userId = playerId,
                    score,
                    won = string.Equals(match.WinnerUserId, playerId, StringComparison.Ordinal),
                    playedAt,
                }, JsonOptions),
                $"{match.MatchId}:{playerId}"));
        }

        foreach (var playerId in match.Players())
        {
            rows.Add(Event(match, playerId, "MATCH_COMPLETED", match.WinnerUserId, match.Draw));
        }

        return rows;
    }

    private static MatchDelivery Event(Match match, string playerId, string type, string? winnerUserId, bool? draw)
    {
        var eventId = Guid.NewGuid();

        var payload = JsonSerializer.Serialize(new
        {
            eventId,
            type,
            userId = playerId,
            payload = new
            {
                matchId = match.MatchId,
                winnerUserId,
                draw,
            },
        }, JsonOptions);

        return new MatchDelivery(MatchDelivery.Notification, EventOperation, payload, eventId.ToString());
    }

    private static IEnumerable<string> Players(this Match match) => [match.PlayerAId, match.PlayerBId];
}
