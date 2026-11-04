using QuizArena.QuestionBank.Domain.Entities;

namespace QuizArena.QuestionBank.Domain.Repositories;

public interface IQuestionRepository
{
    Task<IReadOnlyList<Question>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Question question, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Question>> GenerateAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuestionSnapshot>> GetExactSnapshotsAsync(int count, CancellationToken cancellationToken = default);
}
