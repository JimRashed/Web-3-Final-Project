using Microsoft.EntityFrameworkCore;
using QuizArena.Lobby.Data.Entities;

namespace QuizArena.Lobby.Data.Context;

/// <summary>
/// Owns the Lobby service's own SQL Server database (QuizArenaLobbyDb); no other service reads or writes it.
/// Entity sets and IEntityTypeConfiguration classes in this assembly are applied automatically.
/// </summary>
public sealed class LobbyDbContext(DbContextOptions<LobbyDbContext> options) : DbContext(options)
{
    public DbSet<ActiveLobbyRow> ActiveLobbies => Set<ActiveLobbyRow>();
    public DbSet<LobbyPlayerRow> LobbyPlayers => Set<LobbyPlayerRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LobbyDbContext).Assembly);
    }
}
