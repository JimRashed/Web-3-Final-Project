namespace QuizArena.Domain.QuestionBank.Entities;

public sealed class Question
{
    public Guid Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
