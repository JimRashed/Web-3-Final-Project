namespace QuizArena.Domain.QuestionBank.Entities;

public sealed class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
