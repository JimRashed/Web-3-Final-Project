using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Gameplay.Entities;

namespace QuizArena.Data.Context;

public class GameplayContext : DbContext
{
    public GameplayContext(DbContextOptions<GameplayContext> options) : base(options) { }

    public DbSet<ActiveMatch> ActiveMatches => Set<ActiveMatch>();
    public DbSet<PlayerState> PlayerStates => Set<PlayerState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActiveMatch>().HasKey(m => m.MatchId);
    }
}