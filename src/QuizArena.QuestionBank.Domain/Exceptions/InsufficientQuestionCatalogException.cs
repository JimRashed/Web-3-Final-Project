namespace QuizArena.QuestionBank.Domain.Exceptions;

public sealed class InsufficientQuestionCatalogException(int requested, int available)
    : InvalidOperationException($"Requested {requested} questions, but only {available} distinct questions are available.");
