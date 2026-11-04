namespace QuizArena.QuestionBank.Api.Contracts;

/// <summary>Public catalog question. Answer material is deliberately absent.</summary>
public sealed record QuestionResponse(Guid Id, string Prompt, string Category, string[] Options);
