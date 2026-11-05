using FluentAssertions;
using QuizArena.Gameplay.Api.Services.Outbox;
using Xunit;

namespace QuizArena.Gameplay.UnitTests.Outbox;

public sealed class DeliveryRetryPolicyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(7, 64)]
    [InlineData(8, 128)]
    public void DelayBefore_WithoutRetryAfter_DoublesEachAttempt(int attempt, int expectedSeconds)
    {
        // Act
        var delay = DeliveryRetryPolicy.DelayBefore(attempt);

        // Assert
        delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void DelayBefore_WithJitter_StaysWithinOneSecond()
    {
        // Act
        var delay = DeliveryRetryPolicy.DelayBefore(3, jitterSeconds: 0.9);

        // Assert
        delay.Should().Be(TimeSpan.FromSeconds(4.9));
    }

    [Fact]
    public void DelayBefore_WithRetryAfter_HonoursButBoundsTheReceiver()
    {
        // Act
        var honoured = DeliveryRetryPolicy.DelayBefore(1, TimeSpan.FromSeconds(42));
        var bounded = DeliveryRetryPolicy.DelayBefore(1, TimeSpan.FromHours(2));

        // Assert
        honoured.Should().Be(TimeSpan.FromSeconds(42));
        bounded.Should().Be(DeliveryRetryPolicy.MaxDelay);
    }

    [Fact]
    public void DelayBefore_IgnoresNegativeOrZeroRetryAfter()
    {
        DeliveryRetryPolicy.DelayBefore(2, TimeSpan.FromSeconds(-5)).Should().Be(TimeSpan.FromSeconds(2));
        DeliveryRetryPolicy.DelayBefore(2, TimeSpan.Zero).Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MaxAttempts_IsTheDocumentedEight()
    {
        DeliveryRetryPolicy.MaxAttempts.Should().Be(8);
        DeliveryRetryPolicy.MaxDelay.Should().Be(TimeSpan.FromSeconds(300));
    }
}
