using System.Text.Json;
using FluentAssertions;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Repositories;
using Xunit;

namespace QuizArena.Gameplay.UnitTests.Services;

public sealed class MatchDeliveriesTests
{
    private static readonly Guid[] QuestionIds =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555")
    ];

    private static Match NewMatch()
    {
        var snapshots = QuestionIds
            .Select(id => QuestionSnapshot.Create(id, $"Question {id}", ["A", "B", "C", "D"], "A"))
            .ToArray();

        return Match.Create("match-1", "lobby-1", "player-a", "player-b", snapshots, DateTime.UtcNow);
    }

    private static Match FinishedMatch(int playerACorrect = 5, int playerBCorrect = 3)
    {
        var match = NewMatch();

        for (var round = 0; round < Match.RequiredQuestionCount; round++)
        {
            match.SubmitAnswer("player-a", QuestionIds[round], round < playerACorrect ? "A" : "B", DateTime.UtcNow);
            match.SubmitAnswer("player-b", QuestionIds[round], round < playerBCorrect ? "A" : "B", DateTime.UtcNow);
        }

        return match;
    }

    private static bool HasNoWinner(JsonElement payload) =>
        !payload.GetProperty("payload").TryGetProperty("winnerUserId", out _);

    [Fact]
    public void OnCreated_EnqueuesOneEventPerPlayer()
    {
        // Act
        var deliveries = MatchDeliveries.OnCreated(NewMatch());

        // Assert
        deliveries.Should().HaveCount(2);
        deliveries.Should().OnlyContain(delivery => delivery.Destination == MatchDelivery.Notification);
        deliveries.Select(delivery => delivery.IdempotencyKey).Should().OnlyHaveUniqueItems();

        var payloads = deliveries.Select(delivery => JsonDocument.Parse(delivery.PayloadJson).RootElement).ToArray();
        payloads.Select(payload => payload.GetProperty("type").GetString()).Should().OnlyContain(type => type == "MATCH_CREATED");
        payloads.Select(payload => payload.GetProperty("userId").GetString()).Should().BeEquivalentTo(["player-a", "player-b"]);
        payloads.Should().OnlyContain(payload => payload.GetProperty("payload").GetProperty("matchId").GetString() == "match-1");
        payloads.Should().OnlyContain(payload => HasNoWinner(payload));
    }

    [Fact]
    public void OnCompleted_EnqueuesTwoResultsAndTwoEvents()
    {
        // Act
        var deliveries = MatchDeliveries.OnCompleted(FinishedMatch());

        // Assert
        deliveries.Should().HaveCount(4);
        deliveries.Count(delivery => delivery.Destination == MatchDelivery.Statistics).Should().Be(2);
        deliveries.Count(delivery => delivery.Destination == MatchDelivery.Notification).Should().Be(2);

        var results = deliveries
            .Where(delivery => delivery.Destination == MatchDelivery.Statistics)
            .Select(delivery => JsonDocument.Parse(delivery.PayloadJson).RootElement)
            .ToDictionary(result => result.GetProperty("userId").GetString()!);

        results["player-a"].GetProperty("score").GetInt32().Should().Be(5);
        results["player-a"].GetProperty("won").GetBoolean().Should().BeTrue();
        results["player-b"].GetProperty("score").GetInt32().Should().Be(3);
        results["player-b"].GetProperty("won").GetBoolean().Should().BeFalse();
        results["player-a"].GetProperty("matchId").GetString().Should().Be("match-1");
        results["player-a"].GetProperty("playedAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void OnCompleted_WhenTheMatchIsDrawn_ReportsNoWinner()
    {
        // Act
        var deliveries = MatchDeliveries.OnCompleted(FinishedMatch(playerACorrect: 4, playerBCorrect: 4));

        // Assert
        var events = deliveries
            .Where(delivery => delivery.Destination == MatchDelivery.Notification)
            .Select(delivery => JsonDocument.Parse(delivery.PayloadJson).RootElement)
            .ToArray();

        events.Should().OnlyContain(payload => payload.GetProperty("payload").GetProperty("draw").GetBoolean());
        events.Should().OnlyContain(payload => HasNoWinner(payload));
    }

    [Fact]
    public void OnCompleted_RequiresACompletionTimestamp()
    {
        // Arrange — an active match has no completion time.
        var act = () => MatchDeliveries.OnCompleted(NewMatch());

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Deliveries_CarryStableIdempotencyKeys()
    {
        // Arrange
        var match = FinishedMatch();

        // Act
        var first = MatchDeliveries.OnCompleted(match);
        var second = MatchDeliveries.OnCompleted(match);

        // Assert — results deduplicate by (matchId, userId); events by their generated id.
        var resultKeys = first.Where(row => row.Destination == MatchDelivery.Statistics).Select(row => row.IdempotencyKey).ToArray();
        resultKeys.Should().BeEquivalentTo(["match-1:player-a", "match-1:player-b"]);
        second.Where(row => row.Destination == MatchDelivery.Statistics).Select(row => row.IdempotencyKey)
            .Should().BeEquivalentTo(resultKeys);
        first.Select(row => row.IdempotencyKey).Should().OnlyHaveUniqueItems();
    }
}
