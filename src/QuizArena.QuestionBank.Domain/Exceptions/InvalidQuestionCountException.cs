namespace QuizArena.QuestionBank.Domain.Exceptions;

public sealed class InvalidQuestionCountException(int count)
    : ArgumentOutOfRangeException(nameof(count), count, "Question count must be between 1 and 50.");
