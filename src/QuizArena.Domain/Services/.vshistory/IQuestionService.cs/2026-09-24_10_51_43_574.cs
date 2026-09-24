using QuizArena.Domain.Contracts;

namespace QuizArena.Domain.Services;

public interface IQuestionService
{
    Task<IReadOnlyList<QuestionDto>> GenerateAsync(int count, CancellationToken cancellationToken = default);
}
