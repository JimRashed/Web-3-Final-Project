using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Services;

public sealed class QuestionService(IQuestionRepository questionRepository) : IQuestionService
{
    public async Task<IReadOnlyList<QuestionDto>> GenerateAsync(int count, CancellationToken cancellationToken = default)
    {
        var questions = await questionRepository.GetRandomAsync(count, cancellationToken);
        return questions
            .Select(question => new QuestionDto(question.Id, question.Prompt, question.Category.Name))
            .ToList();
    }
}
