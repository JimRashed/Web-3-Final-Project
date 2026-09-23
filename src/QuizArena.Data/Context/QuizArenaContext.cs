using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Lobby.Entities;
using QuizArena.Domain.QuestionBank.Entities;

namespace QuizArena.Data.Context;

public sealed class QuizArenaContext(DbContextOptions<QuizArenaContext> options) : DbContext(options)
{
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ActiveLobby> ActiveLobbies => Set<ActiveLobby>();
    public DbSet<LobbyPlayer> LobbyPlayers => Set<LobbyPlayer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(question => question.Id);
            entity.Property(question => question.Prompt).IsRequired();
            entity.Property(question => question.CorrectAnswer).IsRequired();
            entity.HasOne(question => question.Category)
                .WithMany(category => category.Questions)
                .HasForeignKey(question => question.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).IsRequired();
        });

        modelBuilder.Entity<ActiveLobby>(entity =>
        {
            entity.HasKey(lobby => lobby.Id);
            entity.HasIndex(lobby => lobby.JoinCode).IsUnique();
            entity.Property(lobby => lobby.JoinCode).IsRequired();
            entity.Property(lobby => lobby.HostId).IsRequired();
            entity.Property(lobby => lobby.Status).IsRequired();
            entity.HasMany(lobby => lobby.Players)
                .WithOne(player => player.Lobby)
                .HasForeignKey(player => player.LobbyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LobbyPlayer>(entity =>
        {
            entity.HasKey(player => player.Id);
            entity.Property(player => player.PlayerId).IsRequired();
        });
    }
}
