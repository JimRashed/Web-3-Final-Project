using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Lobby.Data.Entities;

namespace QuizArena.Lobby.Data.Configurations;

public sealed class LobbyPlayerConfiguration : IEntityTypeConfiguration<LobbyPlayerRow>
{
    public void Configure(EntityTypeBuilder<LobbyPlayerRow> builder)
    {
        builder.ToTable("LobbyPlayers");
        builder.HasKey(player => player.Id);
        builder.Property(player => player.PlayerId).IsRequired().HasMaxLength(200).UseCollation("Latin1_General_100_BIN2");
        builder.HasIndex(player => new { player.LobbyId, player.PlayerId }).IsUnique().HasDatabaseName("IX_LobbyPlayers_LobbyId_PlayerId");
    }
}
