using FluentAssertions;
using Moq;
using QuizArena.QuestionBank.Api.Contracts;
using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.Api.Services;
using QuizArena.QuestionBank.Domain.Entities;
using QuizArena.QuestionBank.Domain.Exceptions;
using QuizArena.QuestionBank.Domain.Repositories;
using Xunit;

namespace QuizArena.QuestionBank.UnitTests.Services;

public sealed class QuestionCatalogServiceTests
{
    private static readonly AuthenticatedActor Player = new("issuer", "opaque-player", ActorKind.User);
    private static readonly AuthenticatedActor Machine = new("issuer", "gameplay", ActorKind.Machine);
    private readonly Mock<IQuestionRepository> _questions = new(MockBehavior.Strict);

    [Fact]
    public async Task GenerateAsync_ValidSelection_MapsPublicFieldsAndCopiesOptions()
    {
        // Arrange
        var question = CreateQuestion();
        using var cancellation = new CancellationTokenSource();
        _questions.Setup(repository => repository.GenerateAsync(10, cancellation.Token)).ReturnsAsync(new[] { question });
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        var result = await sut.GenerateAsync(Player, 10, cancellation.Token);

        // Assert
        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new QuestionResponse(
            question.Id, question.Prompt, question.Category, question.Options.ToArray()));
        result[0].Options[0] = "Changed";
        question.Options[0].Should().Be("Mars");
        _questions.Verify(repository => repository.GenerateAsync(10, cancellation.Token), Times.Once);
        _questions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public async Task GenerateAsync_BoundaryCountAndEmptyCatalog_ReturnsEmpty(int count)
    {
        // Arrange
        _questions.Setup(repository => repository.GenerateAsync(count, CancellationToken.None)).ReturnsAsync(Array.Empty<Question>());
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        var result = await sut.GenerateAsync(Player, count, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _questions.Verify(repository => repository.GenerateAsync(count, CancellationToken.None), Times.Once);
        _questions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(int.MaxValue)]
    public async Task GenerateAsync_InvalidCount_RejectsBeforeRepositoryAccess(int count)
    {
        // Arrange
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        Func<Task> act = () => sut.GenerateAsync(Player, count, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidQuestionCountException>();
        _questions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateSnapshotAsync_FiveQuestions_MapsAnswerMaterialAndCopiesOptions()
    {
        // Arrange
        var snapshots = Enumerable.Range(0, 5).Select(_ => new QuestionSnapshot(CreateQuestion())).ToArray();
        using var cancellation = new CancellationTokenSource();
        _questions.Setup(repository => repository.GetExactSnapshotsAsync(5, cancellation.Token)).ReturnsAsync(snapshots);
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        var result = await sut.CreateSnapshotAsync(Machine, new QuestionSetRequest(5), cancellation.Token);

        // Assert
        result.Should().HaveCount(5);
        result.Select(question => question.Id).Should().Equal(snapshots.Select(question => question.Id));
        for (var index = 0; index < result.Count; index++)
        {
            result[index].Should().BeEquivalentTo(snapshots[index]);
        }
        result[0].Options[0] = "Changed";
        snapshots[0].Options[0].Should().Be("Mars");
        _questions.Verify(repository => repository.GetExactSnapshotsAsync(5, cancellation.Token), Times.Once);
        _questions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(51)]
    public async Task CreateSnapshotAsync_InvalidCount_RejectsBeforeRepositoryAccess(int? count)
    {
        // Arrange
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        Func<Task> act = () => sut.CreateSnapshotAsync(Machine, new QuestionSetRequest(count), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        _questions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateSnapshotAsync_NullRequest_RejectsBeforeRepositoryAccess()
    {
        // Arrange
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        Func<Task> act = () => sut.CreateSnapshotAsync(Machine, null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
        _questions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogOperation_NullActor_RejectsBeforeRepositoryAccess(bool internalOperation)
    {
        // Arrange
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        Func<Task> act = () => InvokeAsync(sut, internalOperation, null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
        _questions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogOperation_WrongActorKind_RejectsBeforeRepositoryAccess(bool internalOperation)
    {
        // Arrange
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        Func<Task> act = () => InvokeAsync(sut, internalOperation, internalOperation ? Player : Machine, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _questions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogOperation_PreCancelled_RejectsBeforeRepositoryAccess(bool internalOperation)
    {
        // Arrange
        var sut = new QuestionCatalogService(_questions.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        Func<Task> act = () => InvokeAsync(sut, internalOperation, internalOperation ? Machine : Player, cancellation.Token);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.CancellationToken.Should().Be(cancellation.Token);
        _questions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogOperation_RepositoryFailure_PropagatesOriginalException(bool internalOperation)
    {
        // Arrange
        var failure = new InvalidOperationException("Dependency unavailable");
        ConfigureFailure(internalOperation, failure);
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        Func<Task> act = () => InvokeAsync(sut, internalOperation, internalOperation ? Machine : Player, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogOperation_RepositoryCancellation_PropagatesCancellation(bool internalOperation)
    {
        // Arrange
        var failure = new OperationCanceledException();
        ConfigureFailure(internalOperation, failure);
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        Func<Task> act = () => InvokeAsync(sut, internalOperation, internalOperation ? Machine : Player, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task CreateSnapshotAsync_InsufficientCatalog_PropagatesDomainConflict()
    {
        // Arrange
        var failure = new InsufficientQuestionCatalogException(5, 4);
        _questions.Setup(repository => repository.GetExactSnapshotsAsync(5, CancellationToken.None)).ThrowsAsync(failure);
        var sut = new QuestionCatalogService(_questions.Object);

        // Act
        Func<Task> act = () => sut.CreateSnapshotAsync(Machine, new QuestionSetRequest(5), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InsufficientQuestionCatalogException>()).Which.Should().BeSameAs(failure);
    }

    private void ConfigureFailure(bool internalOperation, Exception failure)
    {
        if (internalOperation)
            _questions.Setup(repository => repository.GetExactSnapshotsAsync(5, CancellationToken.None)).ThrowsAsync(failure);
        else
            _questions.Setup(repository => repository.GenerateAsync(5, CancellationToken.None)).ThrowsAsync(failure);
    }

    private static Task InvokeAsync(QuestionCatalogService sut, bool internalOperation, AuthenticatedActor actor, CancellationToken cancellationToken)
        => internalOperation
            ? sut.CreateSnapshotAsync(actor, new QuestionSetRequest(5), cancellationToken)
            : sut.GenerateAsync(actor, 5, cancellationToken);

    private static Question CreateQuestion() => new(Guid.NewGuid(), "Red planet?", "Science", ["Mars", "Venus", "Earth", "Jupiter"], "Mars");
}
