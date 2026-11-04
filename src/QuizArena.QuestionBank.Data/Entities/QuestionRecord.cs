namespace QuizArena.QuestionBank.Data.Entities;

public sealed class QuestionRecord
{
    public Guid Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public CategoryRecord Category { get; set; } = null!;
    public string CorrectAnswer { get; set; } = string.Empty;
    public List<QuestionOptionRecord> Options { get; set; } = [];
}
