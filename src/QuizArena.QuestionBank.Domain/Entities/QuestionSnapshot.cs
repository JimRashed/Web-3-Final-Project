namespace QuizArena.QuestionBank.Domain.Entities;

public sealed class QuestionSnapshot
{
    public QuestionSnapshot(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);

        Id = question.Id;
        Prompt = question.Prompt;
        Category = question.Category;
        Options = Array.AsReadOnly(question.Options.ToArray());
        CorrectAnswer = question.CorrectAnswer;
    }

    public Guid Id { get; }
    public string Prompt { get; }
    public string Category { get; }
    public IReadOnlyList<string> Options { get; }
    public string CorrectAnswer { get; }
}
