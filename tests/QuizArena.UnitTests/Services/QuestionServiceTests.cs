using FluentAssertions;
using Moq;
using QuizArena.Api.Services;
using QuizArena.Data.Repositories;
using QuizArena.Domain.QuestionBank.Entities;
using QuizArena.Domain.Contracts;

namespace QuizArena.UnitTests.Services;

public sealed class QuestionServiceTests
{
    [Fact]
    public async Task GenerateAsync_maps_questions_and_forwards_cancellation_token()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Science" };
        var question = new Question { Id = Guid.NewGuid(), Prompt = "Water?", Category = category, CategoryId = category.Id };
        var repository = new Mock<IQuestionRepository>();
        var cancellation = new CancellationTokenSource().Token;
        repository.Setup(x => x.GetRandomAsync(2, cancellation)).ReturnsAsync(new[] { question });

        var result = await new QuestionService(repository.Object).GenerateAsync(2, cancellation);

        result.Should().ContainSingle().Which.Should().Match<QuestionDto>(dto => dto.Id == question.Id && dto.Prompt == "Water?" && dto.Category == "Science");
        repository.Verify(x => x.GetRandomAsync(2, cancellation), Times.Once);
    }
}
