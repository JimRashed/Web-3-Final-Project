using QuizArena.Domain.QuestionBank.Entities;

namespace QuizArena.Data.Repositories;

public interface IQuestionRepository
{
    Task<IReadOnlyList<Question>> GetRandomAsync(int count, CancellationToken cancellationToken = default);
}
