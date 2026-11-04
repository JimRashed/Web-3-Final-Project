using Microsoft.EntityFrameworkCore;
using QuizArena.QuestionBank.Data.Context;
using QuizArena.QuestionBank.Data.Entities;
using QuizArena.QuestionBank.Domain.Entities;
using QuizArena.QuestionBank.Domain.Repositories;
using QuizArena.QuestionBank.Domain.Services;

namespace QuizArena.QuestionBank.Data.Repositories;

public sealed class QuestionRepository(QuestionBankDbContext context) : IQuestionRepository
{
    public async Task<IReadOnlyList<Question>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var records = await context.Questions.AsNoTracking().Include(question => question.Category).Include(question => question.Options).OrderBy(question => question.Id).ToListAsync(cancellationToken);
        return records.Select(record => new Question(record.Id, record.Prompt, record.Category.Name, record.Options.OrderBy(option => option.Position).Select(option => option.Value), record.CorrectAnswer)).ToList().AsReadOnly();
    }

    public async Task AddAsync(Question question, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);
        var category = await context.Categories.SingleOrDefaultAsync(item => item.Name == question.Category, cancellationToken);
        if (category is null)
        {
            category = new CategoryRecord { Id = Guid.NewGuid(), Name = question.Category };
            context.Categories.Add(category);
        }

        context.Questions.Add(new QuestionRecord
        {
            Id = question.Id,
            Prompt = question.Prompt,
            Category = category,
            CorrectAnswer = question.CorrectAnswer,
            Options = question.Options.Select((value, position) => new QuestionOptionRecord { QuestionId = question.Id, Position = position, Value = value }).ToList()
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Question>> GenerateAsync(int count, CancellationToken cancellationToken = default)
    {
        QuestionSelection.ValidateCount(count);
        var catalog = (await GetAllAsync(cancellationToken)).ToArray();
        Random.Shared.Shuffle(catalog);
        return QuestionSelection.SelectUpTo(catalog, count);
    }

    public async Task<IReadOnlyList<QuestionSnapshot>> GetExactSnapshotsAsync(int count, CancellationToken cancellationToken = default)
    {
        QuestionSelection.ValidateCount(count);
        var catalog = (await GetAllAsync(cancellationToken)).ToArray();
        Random.Shared.Shuffle(catalog);
        return QuestionSelection.SelectExactSnapshots(catalog, count);
    }
}
