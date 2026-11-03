using FluentAssertions;
using QuizArena.Notification.Domain.Entities;
using QuizArena.Notification.Domain.Enums;
using QuizArena.Notification.Domain.Exceptions;
using QuizArena.Notification.Domain.Services;
using Xunit;

namespace QuizArena.Notification.UnitTests;

public sealed class EventTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid EventId = Guid.Parse("26c94137-d660-4397-a731-050c2bb07004");
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private const string PlayerA = "player-a";
    private const string MatchId = "match-1";

    private static Event Created() =>
        Event.CreateMatchCreated(EventId, Issuer, PlayerA, MatchId, CreatedAt);

    private static Event Completed(string? winner = PlayerA, bool draw = false) =>
        Event.CreateMatchCompleted(EventId, Issuer, PlayerA, MatchId, winner, draw, CreatedAt);

    [Fact]
    public void CreateMatchCreated_WithValidData_StoresTypedPayload()
    {
        // Act
        var created = Created();

        // Assert
        created.EventId.Should().Be(EventId);
        created.Type.Should().Be(EventType.MatchCreated);
        created.Issuer.Should().Be(Issuer);
        created.UserId.Should().Be(PlayerA);
        created.MatchId.Should().Be(MatchId);
        created.WinnerUserId.Should().BeNull();
        created.Draw.Should().BeFalse();
        created.CreatedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public void CreateMatchCreated_WithEmptyEventId_ThrowsInvalidEventException()
    {
        // Act
        var act = () => Event.CreateMatchCreated(Guid.Empty, Issuer, PlayerA, MatchId, CreatedAt);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Theory]
    [InlineData(" player-a")]
    [InlineData("player-a ")]
    public void CreateMatchCreated_WithPaddedSubject_ThrowsInvalidEventException(string subject)
    {
        // Act
        var act = () => Event.CreateMatchCreated(EventId, Issuer, subject, MatchId, CreatedAt);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void CreateMatchCreated_WithBlankMatchId_ThrowsInvalidEventException()
    {
        // Act
        var act = () => Event.CreateMatchCreated(EventId, Issuer, PlayerA, "   ", CreatedAt);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void CreateMatchCreated_WithLocalTime_ThrowsInvalidEventException()
    {
        // Arrange
        var local = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Local);

        // Act
        var act = () => Event.CreateMatchCreated(EventId, Issuer, PlayerA, MatchId, local);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void CreateMatchCreated_WithDefaultTime_ThrowsInvalidEventException()
    {
        // Act
        var act = () => Event.CreateMatchCreated(EventId, Issuer, PlayerA, MatchId, default);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void CreateMatchCompleted_WithDraw_SetsDrawAndNoWinner()
    {
        // Act
        var completed = Completed(winner: null, draw: true);

        // Assert
        completed.Type.Should().Be(EventType.MatchCompleted);
        completed.Draw.Should().BeTrue();
        completed.WinnerUserId.Should().BeNull();
    }

    [Fact]
    public void CreateMatchCompleted_WithWinner_SetsWinnerAndNoDraw()
    {
        // Act
        var completed = Completed(winner: PlayerA, draw: false);

        // Assert
        completed.Draw.Should().BeFalse();
        completed.WinnerUserId.Should().Be(PlayerA);
    }

    [Fact]
    public void CreateMatchCompleted_WithDrawAndWinner_ThrowsInvalidEventException()
    {
        // Act
        var act = () => Event.CreateMatchCompleted(EventId, Issuer, PlayerA, MatchId, PlayerA, draw: true, CreatedAt);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void CreateMatchCompleted_WithoutDrawOrWinner_ThrowsInvalidEventException()
    {
        // Act
        var act = () => Event.CreateMatchCompleted(EventId, Issuer, PlayerA, MatchId, null, draw: false, CreatedAt);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void HasSameContent_WithIdenticalPayload_ReturnsTrue()
    {
        // Act & Assert
        Created().HasSameContent(Created()).Should().BeTrue();
        Completed().HasSameContent(Completed()).Should().BeTrue();
    }

    [Fact]
    public void HasSameContent_WithDifferentRecipient_ReturnsFalse()
    {
        // Arrange
        var first = Created();
        var second = Event.CreateMatchCreated(EventId, Issuer, "player-b", MatchId, CreatedAt);

        // Act & Assert
        first.HasSameContent(second).Should().BeFalse();
    }

    [Fact]
    public void HasSameContent_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var created = Created();
        var completed = Completed(winner: PlayerA, draw: false);

        // Act & Assert
        created.HasSameContent(completed).Should().BeFalse();
    }

    [Fact]
    public void HasSameContent_WithDifferentOutcome_ReturnsFalse()
    {
        // Arrange
        var won = Completed(winner: PlayerA, draw: false);
        var drawn = Completed(winner: null, draw: true);

        // Act & Assert
        won.HasSameContent(drawn).Should().BeFalse();
    }

    [Fact]
    public void HasSameContent_WithNull_ReturnsFalse()
    {
        // Act & Assert
        Created().HasSameContent(null).Should().BeFalse();
    }
}

public sealed class EventReplayPolicyTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SharedId = Guid.Parse("26c94137-d660-4397-a731-050c2bb07004");
    private const string Issuer = "http://localhost:8080/realms/quizarena";

    private static Event Created(string subject = "player-a") =>
        Event.CreateMatchCreated(SharedId, Issuer, subject, "match-1", CreatedAt);

    [Fact]
    public void Classify_WhenNothingStored_ReturnsNew()
    {
        // Act
        var decision = EventReplayPolicy.Classify(null, Created());

        // Assert
        decision.Should().Be(ReplayDecision.New);
    }

    [Fact]
    public void Classify_WithIdenticalContent_ReturnsIdempotentReplay()
    {
        // Arrange
        var stored = Created();
        var incoming = Event.CreateMatchCreated(SharedId, Issuer, "player-a", "match-1", CreatedAt.AddMinutes(5));

        // Act
        var decision = EventReplayPolicy.Classify(stored, incoming);

        // Assert
        decision.Should().Be(ReplayDecision.IdempotentReplay);
    }

    [Fact]
    public void Classify_WithDifferentRecipient_ReturnsConflict()
    {
        // Arrange
        var stored = Created("player-a");
        var incoming = Created("player-b");

        // Act
        var decision = EventReplayPolicy.Classify(stored, incoming);

        // Assert
        decision.Should().Be(ReplayDecision.Conflict);
    }

    [Fact]
    public void Classify_WithDifferentOutcome_ReturnsConflict()
    {
        // Arrange
        var stored = Event.CreateMatchCompleted(SharedId, Issuer, "player-a", "match-1", "player-a", false, CreatedAt);
        var incoming = Event.CreateMatchCompleted(SharedId, Issuer, "player-a", "match-1", null, true, CreatedAt);

        // Act
        var decision = EventReplayPolicy.Classify(stored, incoming);

        // Assert
        decision.Should().Be(ReplayDecision.Conflict);
    }

    [Fact]
    public void Resolve_WithConflict_ThrowsEventConflictException()
    {
        // Arrange
        var stored = Created("player-a");
        var incoming = Created("player-b");

        // Act
        var act = () => EventReplayPolicy.Resolve(stored, incoming);

        // Assert
        act.Should().Throw<EventConflictException>().Which.EventId.Should().Be(SharedId);
    }

    [Fact]
    public void Resolve_WithIdempotentReplay_ReturnsStoredEvent()
    {
        // Arrange
        var stored = Created();
        var incoming = Event.CreateMatchCreated(SharedId, Issuer, "player-a", "match-1", CreatedAt.AddHours(1));

        // Act
        var resolved = EventReplayPolicy.Resolve(stored, incoming);

        // Assert
        resolved.Should().BeSameAs(stored);
        resolved.CreatedAt.Should().Be(CreatedAt);
    }
}
