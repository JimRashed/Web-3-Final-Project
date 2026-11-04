using FluentAssertions;
using QuizArena.QuestionBank.Domain.Entities;
using QuizArena.QuestionBank.Domain.Exceptions;
using QuizArena.QuestionBank.Domain.Services;
using Xunit;

namespace QuizArena.QuestionBank.UnitTests.Domain;

public sealed class QuestionSelectionTests
{
    private static Question CreateQuestion(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "Prompt", "Category", ["A", "B", "C", "D"], "A");

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void SelectUpTo_OutOfRangeCount_ThrowsTypedException(int count)
    {
        Action select = () => QuestionSelection.SelectUpTo([], count);
        select.Should().Throw<InvalidQuestionCountException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void SelectUpTo_BoundaryCount_IsAccepted(int count)
    {
        QuestionSelection.SelectUpTo([], count).Should().BeEmpty();
    }

    [Fact]
    public void SelectUpTo_DuplicateIds_ReturnsDistinctQuestions()
    {
        var id = Guid.NewGuid();
        var first = CreateQuestion(id);
        var catalog = new[] { first, CreateQuestion(id), CreateQuestion() };

        var selected = QuestionSelection.SelectUpTo(catalog, 3);

        selected.Should().HaveCount(2);
        selected[0].Should().BeSameAs(first);
        selected.Select(question => question.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SelectUpTo_CatalogExceedsCount_ReturnsOnlyRequestedDistinctQuestions()
    {
        // Arrange
        var first = CreateQuestion();
        var second = CreateQuestion();
        var third = CreateQuestion();

        // Act
        var selected = QuestionSelection.SelectUpTo([first, first, second, third], 2);

        // Assert
        selected.Should().Equal(first, second);
    }

    [Fact]
    public void SelectUpTo_NullCatalogItem_RejectsInvalidCatalog()
    {
        Action select = () => QuestionSelection.SelectUpTo([null!], 1);
        select.Should().Throw<InvalidQuestionException>();
    }

    [Fact]
    public void SelectUpTo_NullCatalogItemAfterRequestedCount_RejectsInvalidCatalog()
    {
        var question = CreateQuestion();

        Action select = () => QuestionSelection.SelectUpTo([question, null!], 1);

        select.Should().Throw<InvalidQuestionException>();
    }

    [Fact]
    public void SelectExactSnapshots_InsufficientDistinctQuestions_ThrowsShortage()
    {
        var question = CreateQuestion();
        Action select = () => QuestionSelection.SelectExactSnapshots([question, question], 2);
        select.Should().Throw<InsufficientQuestionCatalogException>();
    }

    [Fact]
    public void SelectExactSnapshots_EmptyCatalog_ThrowsShortage()
    {
        // Arrange
        Question[] catalog = [];

        // Act
        Action select = () => QuestionSelection.SelectExactSnapshots(catalog, 1);

        // Assert
        select.Should().Throw<InsufficientQuestionCatalogException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void SelectExactSnapshots_OutOfRangeCount_ThrowsTypedException(int count)
    {
        Action select = () => QuestionSelection.SelectExactSnapshots([], count);
        select.Should().Throw<InvalidQuestionCountException>();
    }

    [Fact]
    public void SelectExactSnapshots_EnoughQuestions_ReturnsIndependentAnswerBearingCopies()
    {
        var options = new[] { "A", "B", "C", "D" };
        var question = new Question(Guid.NewGuid(), "Prompt", "Category", options, "A");

        var snapshots = QuestionSelection.SelectExactSnapshots([question], 1);
        options[0] = "Changed";

        snapshots.Should().ContainSingle();
        snapshots[0].Id.Should().Be(question.Id);
        snapshots[0].Prompt.Should().Be("Prompt");
        snapshots[0].Category.Should().Be("Category");
        snapshots[0].Options.Should().Equal("A", "B", "C", "D");
        snapshots[0].CorrectAnswer.Should().Be("A");
        Action mutate = () => ((IList<string>)snapshots[0].Options)[0] = "Changed";
        mutate.Should().Throw<NotSupportedException>();
    }
}
