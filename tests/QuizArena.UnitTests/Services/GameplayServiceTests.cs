using FluentAssertions;
using Moq;
using QuizArena.Api.Services;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Gameplay.Entities;
using QuizArena.Domain.Services;

namespace QuizArena.UnitTests.Services;

public sealed class GameplayServiceTests
{
    [Fact]
    public async Task CreateMatchAsync_persists_match_and_notifies_both_players()
    {
        var repository = new Mock<IMatchRepository>();
        repository.Setup(x => x.CreateAsync(It.IsAny<ActiveMatch>())).ReturnsAsync((ActiveMatch match) => match);
        var notifications = new Mock<INotificationService>();

        var result = await new GameplayService(repository.Object, notifications.Object).CreateMatchAsync(new CreateMatchRequest("m1", "a", "b"));

        result.Status.Should().Be("ACTIVE");
        notifications.Verify(x => x.LogEventAsync(It.Is<CreateEventRequest>(e => e.Type == "MATCH_CREATED" && (e.UserId == "a" || e.UserId == "b"))), Times.Exactly(2));
    }

    [Fact]
    public async Task SubmitAnswerAsync_scores_correct_answer_and_finishes_after_five_questions()
    {
        var match = new ActiveMatch { MatchId = "m1", PlayerAId = "a", PlayerBId = "b" };
        var repository = new Mock<IMatchRepository>();
        repository.Setup(x => x.GetAsync("m1")).ReturnsAsync(match);
        repository.Setup(x => x.UpdateAsync(It.IsAny<ActiveMatch>())).ReturnsAsync((ActiveMatch value) => value);
        var notifications = new Mock<INotificationService>();
        var service = new GameplayService(repository.Object, notifications.Object);

        var result = await service.SubmitAnswerAsync("m1", new SubmitAnswerRequest("a", "q-1", "H2O"));

        result.Correct.Should().BeTrue();
        result.PlayerAScore.Should().Be(1);
        match.CurrentQuestionIndex.Should().Be(1);
        repository.Verify(x => x.UpdateAsync(match), Times.Once);
    }

    [Fact]
    public async Task GetNextQuestionAsync_throws_for_missing_match()
    {
        var repository = new Mock<IMatchRepository>();
        repository.Setup(x => x.GetAsync("missing")).ReturnsAsync((ActiveMatch?)null);

        var action = () => new GameplayService(repository.Object, Mock.Of<INotificationService>()).GetNextQuestionAsync("missing");

        await action.Should().ThrowAsync<Exception>().WithMessage("Match not found");
    }

    [Fact]
    public async Task SubmitAnswerAsync_finishes_match_after_five_questions()
    {
        var match = new ActiveMatch { MatchId = "m1", PlayerAId = "a", PlayerBId = "b" };
        var repository = new Mock<IMatchRepository>();
        repository.Setup(x => x.GetAsync("m1")).ReturnsAsync(match);
        repository.Setup(x => x.UpdateAsync(match)).ReturnsAsync(match);
        var notifications = new Mock<INotificationService>();
        var service = new GameplayService(repository.Object, notifications.Object);

        for (var question = 0; question < 5; question++)
        {
            await service.SubmitAnswerAsync("m1", new SubmitAnswerRequest("a", "q-1", "H2O"));
        }

        match.Status.Should().Be("FINISHED");
        match.CurrentQuestionIndex.Should().Be(5);
        notifications.Verify(x => x.LogEventAsync(It.Is<CreateEventRequest>(e => e.Type == "MATCH_COMPLETED")), Times.Exactly(2));
    }
}
