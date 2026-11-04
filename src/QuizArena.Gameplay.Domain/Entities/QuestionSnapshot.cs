using QuizArena.Gameplay.Domain.Exceptions;

namespace QuizArena.Gameplay.Domain.Entities;

/// <summary>
/// Immutable copy of a question bank item captured when the match was created.
/// The correct answer never leaves the domain.
/// </summary>
public sealed class QuestionSnapshot
{
    public const int OptionCount = 4;

    private QuestionSnapshot(Guid questionId, string text, IReadOnlyList<string> options, string correctAnswer)
    {
        QuestionId = questionId;
        Text = text;
        Options = options;
        CorrectAnswer = correctAnswer;
    }

    public Guid QuestionId { get; }

    public string Text { get; }

    public IReadOnlyList<string> Options { get; }

    public string CorrectAnswer { get; }

    public static QuestionSnapshot Create(
        Guid questionId,
        string text,
        IReadOnlyList<string> options,
        string correctAnswer)
    {
        if (questionId == Guid.Empty)
        {
            throw new InvalidMatchException("Question id is required.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidMatchException("Question text is required.");
        }

        if (options is null || options.Count != OptionCount)
        {
            throw new InvalidMatchException($"A question snapshot needs exactly {OptionCount} options.");
        }

        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                throw new InvalidMatchException("Question options must not be blank.");
            }
        }

        if (options.Distinct(StringComparer.Ordinal).Count() != OptionCount)
        {
            throw new InvalidMatchException("Question options must be distinct.");
        }

        if (!options.Contains(correctAnswer, StringComparer.Ordinal))
        {
            throw new InvalidMatchException("The correct answer must be one of the offered options.");
        }

        return new QuestionSnapshot(questionId, text.Trim(), [.. options], correctAnswer);
    }

    public bool IsCorrect(string answer) => string.Equals(answer, CorrectAnswer, StringComparison.Ordinal);
}
