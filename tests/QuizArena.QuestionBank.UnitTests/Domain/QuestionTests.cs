using FluentAssertions;
using QuizArena.QuestionBank.Domain.Entities;
using QuizArena.QuestionBank.Domain.Exceptions;
using Xunit;

namespace QuizArena.QuestionBank.UnitTests.Domain;

public sealed class QuestionTests
{
    private static readonly string[] ValidOptions = ["Mars", "Venus", "Earth", "Jupiter"];

    [Fact]
    public void Constructor_ValidQuestion_CopiesOptionsAndPreservesValues()
    {
        var options = ValidOptions.ToArray();
        var id = Guid.NewGuid();

        var question = new Question(id, "Which planet?", "Astronomy", options, "Mars");
        options[0] = "Changed";

        question.Id.Should().Be(id);
        question.Prompt.Should().Be("Which planet?");
        question.Category.Should().Be("Astronomy");
        question.Options.Should().Equal(ValidOptions);
        question.CorrectAnswer.Should().Be("Mars");
        Action mutate = () => ((IList<string>)question.Options)[0] = "Changed";
        mutate.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Constructor_EmptyId_RejectsQuestion()
    {
        Action create = () => new Question(Guid.Empty, "Prompt", "Category", ValidOptions, "Mars");
        create.Should().Throw<InvalidQuestionException>();
    }

    [Theory]
    [InlineData("", "Category")]
    [InlineData(" ", "Category")]
    [InlineData(null, "Category")]
    [InlineData("Prompt", "")]
    [InlineData("Prompt", " ")]
    [InlineData("Prompt", null)]
    public void Constructor_BlankPromptOrCategory_RejectsQuestion(string? prompt, string? category)
    {
        Action create = () => new Question(Guid.NewGuid(), prompt!, category!, ValidOptions, "Mars");
        create.Should().Throw<InvalidQuestionException>();
    }

    [Fact]
    public void Constructor_OverlengthText_RejectsQuestion()
    {
        Action prompt = () => new Question(Guid.NewGuid(), new string('p', 1001), "Category", ValidOptions, "Mars");
        Action category = () => new Question(Guid.NewGuid(), "Prompt", new string('c', 81), ValidOptions, "Mars");
        Action option = () => new Question(Guid.NewGuid(), "Prompt", "Category", [new string('o', 301), "B", "C", "D"], "B");
        Action answer = () => new Question(Guid.NewGuid(), "Prompt", "Category", ValidOptions, new string('a', 301));

        prompt.Should().Throw<InvalidQuestionException>();
        category.Should().Throw<InvalidQuestionException>();
        option.Should().Throw<InvalidQuestionException>();
        answer.Should().Throw<InvalidQuestionException>();
    }

    [Fact]
    public void Constructor_MaximumLengthText_AcceptsQuestion()
    {
        // Arrange
        var prompt = new string('p', 1000);
        var category = new string('c', 80);
        var answer = new string('a', 300);
        string[] options = [answer, "B", "C", "D"];

        // Act
        var question = new Question(Guid.NewGuid(), prompt, category, options, answer);

        // Assert
        question.Prompt.Should().HaveLength(1000);
        question.Category.Should().HaveLength(80);
        question.CorrectAnswer.Should().HaveLength(300);
    }

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Constructor_InvalidOptions_RejectsQuestion(string[] options)
    {
        Action create = () => new Question(Guid.NewGuid(), "Prompt", "Category", options, "A");
        create.Should().Throw<InvalidQuestionException>();
    }

    public static TheoryData<string[]> InvalidOptions => new()
    {
        { ["A", "B", "C"] },
        { ["A", "B", "C", "D", "E"] },
        { ["A", "B", "", "D"] },
        { ["A", "B", " ", "D"] },
        { ["A", "B", null!, "D"] },
        { ["A", "B", "A", "D"] }
    };

    [Fact]
    public void Constructor_AnswerDoesNotMatchOrdinally_RejectsQuestion()
    {
        Action create = () => new Question(Guid.NewGuid(), "Prompt", "Category", ValidOptions, "mars");
        create.Should().Throw<InvalidQuestionException>();
    }

    [Fact]
    public void Constructor_NullOptions_RejectsQuestion()
    {
        Action create = () => new Question(Guid.NewGuid(), "Prompt", "Category", null!, "Mars");
        create.Should().Throw<InvalidQuestionException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_BlankAnswer_RejectsQuestion(string? answer)
    {
        Action create = () => new Question(Guid.NewGuid(), "Prompt", "Category", ValidOptions, answer!);
        create.Should().Throw<InvalidQuestionException>();
    }

    [Fact]
    public void Constructor_OrdinallyDistinctOptions_AreAccepted()
    {
        var question = new Question(Guid.NewGuid(), "Prompt", "Category", ["Mars", "mars", "Earth", "Venus"], "Mars");
        question.Options.Should().HaveCount(4);
    }
}
