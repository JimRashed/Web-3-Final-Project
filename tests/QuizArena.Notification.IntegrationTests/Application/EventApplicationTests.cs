using FluentAssertions;
using Moq;
using QuizArena.Notification.Api.Contracts;
using QuizArena.Notification.Api.Services;
using QuizArena.Notification.Domain.Entities;
using QuizArena.Notification.Domain.Exceptions;
using QuizArena.Notification.Domain.Repositories;
using Xunit;

namespace QuizArena.Notification.IntegrationTests.Application;

/// <summary>
/// Application-layer rules with mocked repositories: no database, no HTTP.
/// </summary>
public sealed class EventApplicationTests
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private const string Player = "player-a";

    private readonly Mock<IEventRepository> _events = new();
    private readonly Mock<IUserPreferenceRepository> _preferences = new();

    private EventApplication Sut() => new(_events.Object, _preferences.Object);

    [Fact]
    public async Task PublishAsync_WithMatchCreated_MapsStoredEvent()
    {
        // Arrange
        var request = new CreateEventRequest(Guid.NewGuid(), "MATCH_CREATED", Player, new CreateEventPayload("match-1"));
        _events.Setup(repository => repository.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event candidate, CancellationToken _) => candidate);

        // Act
        var response = await Sut().PublishAsync(request, Issuer, CancellationToken.None);

        // Assert
        response.Type.Should().Be("MATCH_CREATED");
        response.UserId.Should().Be(Player);
        response.Payload.MatchId.Should().Be("match-1");
        response.Payload.WinnerUserId.Should().BeNull();
        response.Payload.Draw.Should().BeNull();
    }

    [Fact]
    public async Task PublishAsync_WithMatchCompleted_MapsOutcome()
    {
        // Arrange
        var request = new CreateEventRequest(
            Guid.NewGuid(),
            "MATCH_COMPLETED",
            Player,
            new CreateEventPayload("match-1", Player, false));

        _events.Setup(repository => repository.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event candidate, CancellationToken _) => candidate);

        // Act
        var response = await Sut().PublishAsync(request, Issuer, CancellationToken.None);

        // Assert
        response.Type.Should().Be("MATCH_COMPLETED");
        response.Payload.WinnerUserId.Should().Be(Player);
        response.Payload.Draw.Should().BeFalse();
    }

    [Fact]
    public async Task PublishAsync_WithMatchCreatedCarryingOutcome_Throws()
    {
        // Arrange — MATCH_CREATED must not carry winner or draw.
        var request = new CreateEventRequest(
            Guid.NewGuid(),
            "MATCH_CREATED",
            Player,
            new CreateEventPayload("match-1", Player, false));

        // Act
        var act = () => Sut().PublishAsync(request, Issuer, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidEventException>();
    }

    [Fact]
    public async Task PublishAsync_WithUnknownType_Throws()
    {
        // Arrange
        var request = new CreateEventRequest(Guid.NewGuid(), "FOO_BAR", Player, new CreateEventPayload("match-1"));

        // Act
        var act = () => Sut().PublishAsync(request, Issuer, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidEventException>();
    }

    [Fact]
    public async Task PublishAsync_WithMissingMatchId_Throws()
    {
        // Arrange
        var request = new CreateEventRequest(Guid.NewGuid(), "MATCH_CREATED", Player, new CreateEventPayload(null));

        // Act
        var act = () => Sut().PublishAsync(request, Issuer, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidEventException>();
    }

    [Fact]
    public async Task PublishAsync_WhenRepositoryReportsConflict_Propagates()
    {
        // Arrange
        var request = new CreateEventRequest(Guid.NewGuid(), "MATCH_CREATED", Player, new CreateEventPayload("match-1"));
        _events.Setup(repository => repository.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EventConflictException(request.EventId));

        // Act
        var act = () => Sut().PublishAsync(request, Issuer, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EventConflictException>();
    }

    [Fact]
    public async Task GetPreferencesAsync_WhenPlayerNeverSaved_ReturnsDisabledDefaults()
    {
        // Arrange
        _preferences.Setup(repository => repository.FindAsync(Issuer, Player, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreference?)null);

        // Act
        var response = await Sut().GetPreferencesAsync(Issuer, Player, CancellationToken.None);

        // Assert
        response.EmailEnabled.Should().BeFalse();
        response.PushEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetPreferencesAsync_WhenStored_ReturnsStoredValues()
    {
        // Arrange
        _preferences.Setup(repository => repository.FindAsync(Issuer, Player, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserPreference.Rehydrate(Issuer, Player, true, false));

        // Act
        var response = await Sut().GetPreferencesAsync(Issuer, Player, CancellationToken.None);

        // Assert
        response.EmailEnabled.Should().BeTrue();
        response.PushEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePreferencesAsync_PersistsRequestedFlags()
    {
        // Arrange
        UserPreference? saved = null;
        _preferences.Setup(repository => repository.UpsertAsync(It.IsAny<UserPreference>(), It.IsAny<CancellationToken>()))
            .Callback<UserPreference, CancellationToken>((preference, _) => saved = preference)
            .ReturnsAsync((UserPreference preference, CancellationToken _) => preference);

        // Act
        var response = await Sut().UpdatePreferencesAsync(Issuer, Player, new UpdatePreferencesRequest(true, true), CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.Issuer.Should().Be(Issuer);
        saved.Subject.Should().Be(Player);
        response.EmailEnabled.Should().BeTrue();
        response.PushEnabled.Should().BeTrue();
    }
}
