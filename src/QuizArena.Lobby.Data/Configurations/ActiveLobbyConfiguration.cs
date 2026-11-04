using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Lobby.Data.Entities;

namespace QuizArena.Lobby.Data.Configurations;

public sealed class ActiveLobbyConfiguration : IEntityTypeConfiguration<ActiveLobbyRow>
{
    public void Configure(EntityTypeBuilder<ActiveLobbyRow> builder)
    {
        builder.ToTable("ActiveLobbies");
        builder.HasKey(lobby => lobby.Id);
        builder.Property(lobby => lobby.JoinCode).IsRequired().HasMaxLength(6).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        builder.Property(lobby => lobby.HostId).IsRequired().HasMaxLength(200).UseCollation("Latin1_General_100_BIN2");
        builder.Property(lobby => lobby.Status).IsRequired();
        builder.Property(lobby => lobby.WriteRevision).IsRequired();
        builder.Property(lobby => lobby.RowVersion).IsRowVersion();
        builder.HasIndex(lobby => lobby.JoinCode).IsUnique().HasDatabaseName("IX_ActiveLobbies_JoinCode");
        builder.HasMany(lobby => lobby.Players).WithOne(player => player.Lobby).HasForeignKey(player => player.LobbyId).OnDelete(DeleteBehavior.Cascade);
    }
}
