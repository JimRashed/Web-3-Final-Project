using Microsoft.EntityFrameworkCore;
using QuizArena.Statistics.Domain.Entities;

namespace QuizArena.Statistics.Data.Context;

/// <summary>
/// Owns the Statistics service's own SQL Server database (QuizArenaStatisticsDb); no other service reads or writes it.
/// Entity sets and IEntityTypeConfiguration classes in this assembly are applied automatically.
/// </summary>
public sealed class StatisticsDbContext(DbContextOptions<StatisticsDbContext> options) : DbContext(options)
{
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StatisticsDbContext).Assembly);
    }
}
