using Microsoft.EntityFrameworkCore;

namespace QuizArena.QuestionBank.Data.Context;

/// <summary>
/// Owns the QuestionBank service's own SQL Server database (QuizArenaQuestionBankDb); no other service reads or writes it.
/// Entity sets and IEntityTypeConfiguration classes in this assembly are applied automatically.
/// </summary>
public sealed class QuestionBankDbContext(DbContextOptions<QuestionBankDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuestionBankDbContext).Assembly);
    }
}
