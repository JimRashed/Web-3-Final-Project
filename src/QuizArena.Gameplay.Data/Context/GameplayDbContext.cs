using Microsoft.EntityFrameworkCore;

namespace QuizArena.Gameplay.Data.Context;

/// <summary>
/// Owns the Gameplay service's own SQL Server database (QuizArenaGameplayDb); no other service reads or writes it.
/// Entity sets and IEntityTypeConfiguration classes in this assembly are applied automatically.
/// </summary>
public sealed class GameplayDbContext(DbContextOptions<GameplayDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameplayDbContext).Assembly);
    }
}
