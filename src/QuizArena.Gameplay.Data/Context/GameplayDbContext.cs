using Microsoft.EntityFrameworkCore;
using QuizArena.Gameplay.Data.Records;

namespace QuizArena.Gameplay.Data.Context;

/// <summary>
/// Owns the Gameplay service's own SQL Server database (QuizArenaGameplayDb); no other service reads or writes it.
/// Entity sets and IEntityTypeConfiguration classes in this assembly are applied automatically.
/// </summary>
public sealed class GameplayDbContext(DbContextOptions<GameplayDbContext> options) : DbContext(options)
{
    public DbSet<MatchRecord> Matches => Set<MatchRecord>();

    public DbSet<DeliveryRecord> Deliveries => Set<DeliveryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameplayDbContext).Assembly);
    }
}
