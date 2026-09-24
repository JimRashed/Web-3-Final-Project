using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Gameplay.Entities;
using QuizArena.Domain.Identity.Entities;
using QuizArena.Domain.Lobby.Entities;
using QuizArena.Domain.Notification.Entities;
using QuizArena.Domain.QuestionBank.Entities;
using QuizArena.Domain.Statistics.Entities;

namespace QuizArena.Data.Context;

public sealed class QuizArenaContext(DbContextOptions<QuizArenaContext> options) : DbContext(options)
{
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ActiveLobby> ActiveLobbies => Set<ActiveLobby>();
    public DbSet<LobbyPlayer> LobbyPlayers => Set<LobbyPlayer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<ActiveMatch> ActiveMatches => Set<ActiveMatch>();
    public DbSet<PlayerState> PlayerStates => Set<PlayerState>();
    public DbSet<EventLog> EventLogs => Set<EventLog>();
    public DbSet<UserPref> UserPrefs => Set<UserPref>();

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

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username)
                .IsRequired()
                .HasMaxLength(50);
            entity.HasIndex(user => user.Username).IsUnique();
            entity.Property(user => user.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<MatchResult>(entity =>
        {
            entity.HasKey(result => result.Id);
            entity.Property(result => result.Id).ValueGeneratedOnAdd();
            entity.Property(result => result.MatchId).IsRequired();
            entity.Property(result => result.UserId).IsRequired();
            entity.Property(result => result.Score).IsRequired();
            entity.Property(result => result.Won).IsRequired();
            entity.Property(result => result.PlayedAt).IsRequired();
            entity.HasIndex(result => result.UserId);
            entity.HasIndex(result => new { result.MatchId, result.UserId }).IsUnique();
        });

        modelBuilder.Entity<ActiveMatch>(entity =>
        {
            entity.HasKey(match => match.MatchId);
            entity.Property(match => match.MatchId).HasMaxLength(100);
            entity.Property(match => match.PlayerAId).IsRequired();
            entity.Property(match => match.PlayerBId).IsRequired();
            entity.Property(match => match.Status).IsRequired();
        });

        modelBuilder.Entity<PlayerState>(entity =>
        {
            entity.HasKey(state => state.Id);
            entity.Property(state => state.MatchId).IsRequired();
            entity.Property(state => state.UserId).IsRequired();
            entity.HasIndex(state => new { state.MatchId, state.UserId }).IsUnique();
            entity.HasOne<ActiveMatch>()
                .WithMany()
                .HasForeignKey(state => state.MatchId)
                .HasPrincipalKey(match => match.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventLog>(entity =>
        {
            entity.HasKey(eventLog => eventLog.Id);
            entity.Property(eventLog => eventLog.Type).IsRequired();
            entity.Property(eventLog => eventLog.UserId).IsRequired();
            entity.Property(eventLog => eventLog.PayloadJson).IsRequired();
            entity.Property(eventLog => eventLog.CreatedAt).IsRequired();
            entity.HasIndex(eventLog => eventLog.UserId);
        });

        modelBuilder.Entity<UserPref>(entity =>
        {
            entity.HasKey(pref => pref.Id);
            entity.Property(pref => pref.UserId).IsRequired();
            entity.HasIndex(pref => pref.UserId).IsUnique();
        });
    }
}
