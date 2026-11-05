using Microsoft.EntityFrameworkCore;
using QuizArena.QuestionBank.Data.Entities;
using QuizArena.QuestionBank.Data.Seed;

namespace QuizArena.QuestionBank.Data.Context;

/// <summary>
/// Owns the QuestionBank service's own SQL Server database (QuizArenaQuestionBankDb); no other service reads or writes it.
/// Entity sets and IEntityTypeConfiguration classes in this assembly are applied automatically.
/// </summary>
public sealed class QuestionBankDbContext(DbContextOptions<QuestionBankDbContext> options) : DbContext(options)
{
    public DbSet<QuestionRecord> Questions => Set<QuestionRecord>();
    public DbSet<CategoryRecord> Categories => Set<CategoryRecord>();
    public DbSet<QuestionOptionRecord> QuestionOptions => Set<QuestionOptionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuestionBankDbContext).Assembly);
        QuestionCatalog.Configure(modelBuilder);
    }
}
