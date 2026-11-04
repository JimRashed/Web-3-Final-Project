namespace QuizArena.QuestionBank.Data.Entities;

public sealed class QuestionOptionRecord
{
    public Guid QuestionId { get; set; }
    public int Position { get; set; }
    public string Value { get; set; } = string.Empty;
}
