using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Statistics.Entities;

namespace QuizArena.Data.Context;

public class StatisticsContext : DbContext
{
    public StatisticsContext(DbContextOptions<StatisticsContext> options) : base(options) { }

    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
}