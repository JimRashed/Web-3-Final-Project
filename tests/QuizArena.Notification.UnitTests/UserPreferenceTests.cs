using FluentAssertions;
using QuizArena.Notification.Domain.Entities;
using QuizArena.Notification.Domain.Exceptions;
using Xunit;

namespace QuizArena.Notification.UnitTests;

public sealed class UserPreferenceTests
{
    [Fact]
    public void CreateDefault_DisablesBothChannels()
    {
        // Act
        var preference = UserPreference.CreateDefault("quizarena", "player-a");

        // Assert
        preference.EmailEnabled.Should().BeFalse();
        preference.PushEnabled.Should().BeFalse();
        preference.Issuer.Should().Be("quizarena");
        preference.Subject.Should().Be("player-a");
    }

    [Theory]
    [InlineData("", "player-a")]
    [InlineData("quizarena", " ")]
    public void CreateDefault_WithBlankIdentity_Throws(string issuer, string subject)
    {
        // Act
        var act = () => UserPreference.CreateDefault(issuer, subject);

        // Assert
        act.Should().Throw<NotificationDomainException>();
    }

    [Theory]
    [InlineData(" player-a")]
    [InlineData("player-a ")]
    public void CreateDefault_WithPaddedSubject_Throws(string subject)
    {
        // Act
        var act = () => UserPreference.CreateDefault("quizarena", subject);

        // Assert
        act.Should().Throw<NotificationDomainException>();
    }

    [Fact]
    public void Update_ChangesBothChannels()
    {
        // Arrange
        var preference = UserPreference.CreateDefault("quizarena", "player-a");

        // Act
        preference.Update(emailEnabled: true, pushEnabled: true);

        // Assert
        preference.EmailEnabled.Should().BeTrue();
        preference.PushEnabled.Should().BeTrue();
    }

    [Fact]
    public void Rehydrate_KeepsStoredValues()
    {
        // Act
        var preference = UserPreference.Rehydrate("quizarena", "player-a", emailEnabled: true, pushEnabled: false);

        // Assert
        preference.EmailEnabled.Should().BeTrue();
        preference.PushEnabled.Should().BeFalse();
    }
}
