using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Statistics.Entities;

namespace QuizArena.Data.Context;

public class StatisticsContext : DbContext
{
    public StatisticsContext(DbContextOptions<StatisticsContext> options) : base(options) { }

    public DbSet<MatchResult> MatchResults => Set<MatchResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MatchResult>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Id)
                .ValueGeneratedOnAdd();

            entity.Property(r => r.MatchId).IsRequired();
            entity.Property(r => r.UserId).IsRequired();
            entity.Property(r => r.Score).IsRequired();
            entity.Property(r => r.Won).IsRequired();
            entity.Property(r => r.PlayedAt).IsRequired();

            entity.HasIndex(r => r.UserId);
            entity.HasIndex(r => new { r.MatchId, r.UserId }).IsUnique();
        });
    }
}