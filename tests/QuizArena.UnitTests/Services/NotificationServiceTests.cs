using FluentAssertions;
using Moq;
using QuizArena.Api.Services;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Notification.Entities;
using QuizArena.Domain.Services;

namespace QuizArena.UnitTests.Services;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task LogEventAsync_maps_saved_event()
    {
        var saved = new EventLog { Id = 4, Type = "MATCH_CREATED", UserId = "u1", PayloadJson = "{}", CreatedAt = DateTime.UtcNow };
        var repository = new Mock<IEventRepository>();
        repository.Setup(x => x.AddAsync(It.IsAny<EventLog>())).ReturnsAsync(saved);

        var result = await new NotificationService(repository.Object).LogEventAsync(new CreateEventRequest("MATCH_CREATED", "u1", "{}"));

        result.Should().BeEquivalentTo(saved);
    }

    [Fact]
    public async Task UpdatePrefAsync_upserts_and_maps_preference()
    {
        var repository = new Mock<IEventRepository>();
        repository.Setup(x => x.UpsertPrefAsync(It.IsAny<UserPref>())).ReturnsAsync((UserPref pref) => pref);

        var result = await new NotificationService(repository.Object).UpdatePrefAsync("u1", new UpdatePrefRequest(false, true));

        result.Should().Be(new UserPrefDto("u1", false, true));
        repository.Verify(x => x.UpsertPrefAsync(It.Is<UserPref>(p => p.UserId == "u1" && !p.EmailEnabled && p.PushEnabled)), Times.Once);
    }

    [Fact]
    public async Task GetUserEventsAsync_returns_empty_for_unknown_user()
    {
        var repository = new Mock<IEventRepository>();
        repository.Setup(x => x.GetByUserAsync("missing")).ReturnsAsync(new List<EventLog>());

        (await new NotificationService(repository.Object).GetUserEventsAsync("missing")).Should().BeEmpty();
    }

    [Fact]
    public async Task LogEventAsync_propagates_repository_failure()
    {
        var repository = new Mock<IEventRepository>();
        repository.Setup(x => x.AddAsync(It.IsAny<EventLog>()))
            .ThrowsAsync(new InvalidOperationException("event store unavailable"));

        var action = () => new NotificationService(repository.Object)
            .LogEventAsync(new CreateEventRequest("TEST", "u1", "{}"));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("event store unavailable");
    }
}
