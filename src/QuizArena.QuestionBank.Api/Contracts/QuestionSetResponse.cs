namespace QuizArena.QuestionBank.Api.Contracts;

/// <summary>
/// One machine-only answer-bearing snapshot (OpenAPI QuestionSnapshot).
/// The question-set response is an array of these elements, never a public question response.
/// </summary>
public sealed record QuestionSetResponse(Guid Id, string Prompt, string Category, string[] Options, string CorrectAnswer);
