namespace QuizArena.Domain.Contracts;

public sealed record QuestionDto(Guid Id, string Prompt, string Category);
