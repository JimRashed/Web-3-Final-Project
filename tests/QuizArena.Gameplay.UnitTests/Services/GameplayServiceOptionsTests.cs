using FluentAssertions;
using QuizArena.Gameplay.Api.Services;
using Xunit;

namespace QuizArena.Gameplay.UnitTests.Services;

/// <summary>The startup rule for <c>ServiceUrls:*</c> accepts only absolute http or https addresses.</summary>
public sealed class GameplayServiceOptionsTests
{
    [Theory]
    [InlineData("http://x")]
    [InlineData("https://x/")]
    [InlineData("http://localhost:5104")]
    public void IsHttpUri_WithAbsoluteHttpOrHttps_ReturnsTrue(string value)
    {
        // Act
        var result = GameplayServiceOptions.IsHttpUri(value);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/internal/lobbies")]
    [InlineData("ftp://x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not-a-url")]
    public void IsHttpUri_WithBlankRelativeOrOtherScheme_ReturnsFalse(string? value)
    {
        // Act
        var result = GameplayServiceOptions.IsHttpUri(value);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Messages_NameTheKeyOnly()
    {
        // Assert
        GameplayServiceOptions.Messages.Lobby.Should().StartWith("ServiceUrls:Lobby ");
        GameplayServiceOptions.Messages.QuestionBank.Should().StartWith("ServiceUrls:QuestionBank ");
        GameplayServiceOptions.Messages.Statistics.Should().StartWith("ServiceUrls:Statistics ");
        GameplayServiceOptions.Messages.Notification.Should().StartWith("ServiceUrls:Notification ");
        GameplayServiceOptions.Messages.MachineClientSecret.Should().StartWith("Authentication:MachineClientSecret ");
    }
}
