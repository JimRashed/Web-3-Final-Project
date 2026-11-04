using QuizArena.QuestionBank.Domain.Exceptions;

namespace QuizArena.QuestionBank.Domain.Entities;

public sealed class Question
{
    public Question(Guid id, string prompt, string category, IEnumerable<string> options, string correctAnswer)
    {
        if (id == Guid.Empty)
            throw new InvalidQuestionException("Question ID cannot be empty.");

        Id = id;
        Prompt = ValidateText(prompt, 1000, "Prompt");
        Category = ValidateText(category, 80, "Category");
        CorrectAnswer = ValidateText(correctAnswer, 300, "Correct answer");

        if (options is null)
            throw new InvalidQuestionException("Options are required.");

        var copiedOptions = options.ToArray();
        if (copiedOptions.Length != 4)
            throw new InvalidQuestionException("A question requires exactly four options.");

        foreach (var option in copiedOptions)
            ValidateText(option, 300, "Option");

        if (copiedOptions.Distinct(StringComparer.Ordinal).Count() != 4)
            throw new InvalidQuestionException("Options must be distinct using ordinal comparison.");

        if (copiedOptions.Count(option => string.Equals(option, CorrectAnswer, StringComparison.Ordinal)) != 1)
            throw new InvalidQuestionException("Correct answer must match exactly one option using ordinal comparison.");

        Options = Array.AsReadOnly(copiedOptions);
    }

    public Guid Id { get; }
    public string Prompt { get; }
    public string Category { get; }
    public IReadOnlyList<string> Options { get; }
    public string CorrectAnswer { get; }

    private static string ValidateText(string? value, int maximumLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            throw new InvalidQuestionException($"{name} must be nonblank and at most {maximumLength} characters.");

        return value;
    }
}
