using QuizArena.QuestionBank.Domain.Entities;
using QuizArena.QuestionBank.Domain.Exceptions;

namespace QuizArena.QuestionBank.Domain.Services;

public static class QuestionSelection
{
    public static IReadOnlyList<Question> SelectUpTo(IEnumerable<Question> catalog, int count)
    {
        ValidateCount(count);
        ArgumentNullException.ThrowIfNull(catalog);

        var selected = new List<Question>(count);
        var seen = new HashSet<Guid>();
        foreach (var question in catalog)
        {
            if (question is null)
                throw new InvalidQuestionException("Catalog contains a null question.");

            if (selected.Count < count && seen.Add(question.Id))
                selected.Add(question);
        }

        return selected.AsReadOnly();
    }

    public static IReadOnlyList<QuestionSnapshot> SelectExactSnapshots(IEnumerable<Question> catalog, int count)
    {
        var selected = SelectUpTo(catalog, count);
        if (selected.Count < count)
            throw new InsufficientQuestionCatalogException(count, selected.Count);

        return selected.Select(question => new QuestionSnapshot(question)).ToList().AsReadOnly();
    }

    public static void ValidateCount(int count)
    {
        if (count is < 1 or > 50)
            throw new InvalidQuestionCountException(count);
    }
}
