namespace QuizArena.QuestionBank.Domain.Exceptions;

public sealed class InvalidQuestionException(string message) : ArgumentException(message);
