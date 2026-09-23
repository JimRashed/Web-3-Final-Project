using Microsoft.EntityFrameworkCore;
using QuizArena.Data.Context;
using QuizArena.Domain.QuestionBank.Entities;

namespace QuizArena.Data.Repositories;

public sealed class QuestionRepository(QuizArenaContext context) : IQuestionRepository
{
    public async Task<IReadOnlyList<Question>> GetRandomAsync(int count, CancellationToken cancellationToken = default)
    {
        return await context.Questions
            .Include(question => question.Category)
            .OrderBy(_ => Guid.NewGuid())
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
