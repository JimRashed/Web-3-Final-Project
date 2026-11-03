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
    private const string ValidPayload = """{"matchId":"26c94137-d660-4397-a731-050c2bb07001","draw":false}""";

    [Fact]
    public void Create_WithValidData_StoresNormalisedEvent()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        // Act
        var created = Event.Create("MATCH_COMPLETED", "player-a", ValidPayload, CreatedAt, eventId);

        // Assert
        created.EventId.Should().Be(eventId);
        created.Type.Should().Be("MATCH_COMPLETED");
        created.UserId.Should().Be("player-a");
        created.PayloadJson.Should().Be(ValidPayload);
        created.CreatedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public void Create_WithoutEventId_GeneratesIdentifier()
    {
        // Act
        var created = Event.Create("MATCH_COMPLETED", "player-a", ValidPayload, CreatedAt);

        // Assert
        created.EventId.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankType_ThrowsInvalidEventException(string type)
    {
        // Act
        var act = () => Event.Create(type, "player-a", ValidPayload, CreatedAt);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void Create_WithLowerCaseType_ThrowsInvalidEventException()
    {
        // Act
        var act = () => Event.Create("match_completed", "player-a", ValidPayload, CreatedAt);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void Create_WithBlankUser_ThrowsInvalidEventException()
    {
        // Act
        var act = () => Event.Create("MATCH_COMPLETED", " ", ValidPayload, CreatedAt);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    public void Create_WithInvalidOrNonObjectPayload_ThrowsInvalidEventException(string payload)
    {
        // Act
        var act = () => Event.Create("MATCH_COMPLETED", "player-a", payload, CreatedAt);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void Create_WithLocalTime_ThrowsInvalidEventException()
    {
        // Arrange
        var local = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Local);

        // Act
        var act = () => Event.Create("MATCH_COMPLETED", "player-a", ValidPayload, local);

        // Assert
        act.Should().Throw<InvalidEventException>();
    }

    [Fact]
    public void Create_WithDifferentWhitespace_CanonicalisesPayload()
    {
        // Act
        var compact = Event.Create("MATCH_COMPLETED", "player-a", """{"a":1}""", CreatedAt);
        var spaced = Event.Create("MATCH_COMPLETED", "player-a", """{ "a" : 1 }""", CreatedAt);

        // Assert
        compact.PayloadJson.Should().Be("""{"a":1}""");
        spaced.PayloadJson.Should().Be(compact.PayloadJson);
        compact.HasSamePayload(spaced).Should().BeTrue();
    }

    [Fact]
    public void HasSamePayload_WithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var first = Event.Create("MATCH_COMPLETED", "player-a", """{"a":1}""", CreatedAt);
        var second = Event.Create("MATCH_COMPLETED", "player-a", """{"a":2}""", CreatedAt);

        // Act
        var result = first.HasSamePayload(second);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasSamePayload_WithNull_ReturnsFalse()
    {
        // Arrange
        var created = Event.Create("MATCH_COMPLETED", "player-a", ValidPayload, CreatedAt);

        // Act
        var result = created.HasSamePayload(null);

        // Assert
        result.Should().BeFalse();
    }
}

public sealed class EventReplayPolicyTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SharedId = Guid.Parse("26c94137-d660-4397-a731-050c2bb07004");

    [Fact]
    public void Classify_WhenNothingStored_ReturnsNew()
    {
        // Arrange
        var incoming = Event.Create("MATCH_COMPLETED", "player-a", """{"a":1}""", CreatedAt, SharedId);

        // Act
        var decision = EventReplayPolicy.Classify(null, incoming);

        // Assert
        decision.Should().Be(ReplayDecision.New);
    }

    [Fact]
    public void Classify_WithIdenticalPayload_ReturnsIdempotentReplay()
    {
        // Arrange
        var stored = Event.Create("MATCH_COMPLETED", "player-a", """{"a":1}""", CreatedAt, SharedId);
        var incoming = Event.Create("MATCH_COMPLETED", "player-a", """{ "a" : 1 }""", CreatedAt.AddMinutes(5), SharedId);

        // Act
        var decision = EventReplayPolicy.Classify(stored, incoming);

        // Assert
        decision.Should().Be(ReplayDecision.IdempotentReplay);
    }

    [Fact]
    public void Classify_WithDifferentPayload_ReturnsConflict()
    {
        // Arrange
        var stored = Event.Create("MATCH_COMPLETED", "player-a", """{"a":1}""", CreatedAt, SharedId);
        var incoming = Event.Create("MATCH_COMPLETED", "player-a", """{"a":2}""", CreatedAt, SharedId);

        // Act
        var decision = EventReplayPolicy.Classify(stored, incoming);

        // Assert
        decision.Should().Be(ReplayDecision.Conflict);
    }

    [Fact]
    public void Resolve_WithConflict_ThrowsEventConflictException()
    {
        // Arrange
        var stored = Event.Create("MATCH_COMPLETED", "player-a", """{"a":1}""", CreatedAt, SharedId);
        var incoming = Event.Create("MATCH_COMPLETED", "player-a", """{"a":2}""", CreatedAt, SharedId);

        // Act
        var act = () => EventReplayPolicy.Resolve(stored, incoming);

        // Assert
        act.Should().Throw<EventConflictException>().Which.EventId.Should().Be(SharedId);
    }

    [Fact]
    public void Resolve_WithIdempotentReplay_ReturnsStoredEvent()
    {
        // Arrange
        var stored = Event.Create("MATCH_COMPLETED", "player-a", """{"a":1}""", CreatedAt, SharedId);
        var incoming = Event.Create("MATCH_COMPLETED", "player-a", """{"a":1}""", CreatedAt.AddHours(1), SharedId);

        // Act
        var resolved = EventReplayPolicy.Resolve(stored, incoming);

        // Assert
        resolved.Should().BeSameAs(stored);
        resolved.CreatedAt.Should().Be(CreatedAt);
    }
}
