using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Gameplay.Data.Records;
using QuizArena.Gameplay.Domain.Entities;

namespace QuizArena.Gameplay.Data.Configurations;

public sealed class MatchConfiguration : IEntityTypeConfiguration<MatchRecord>
{
    /// <summary>Binary collation: ids compare exactly (case-sensitive, ordinal), as in the domain.</summary>
    public const string ExactCollation = "Latin1_General_100_BIN2";

    public void Configure(EntityTypeBuilder<MatchRecord> builder)
    {
        builder.ToTable("Matches");

        builder.HasKey(record => record.MatchId);
        builder.Property(record => record.MatchId).HasMaxLength(100).UseCollation(ExactCollation);

        builder.Property(record => record.LobbyId).HasMaxLength(100).UseCollation(ExactCollation).IsRequired();
        builder.Property(record => record.PlayerAId).HasMaxLength(200).UseCollation(ExactCollation).IsRequired();
        builder.Property(record => record.PlayerBId).HasMaxLength(200).UseCollation(ExactCollation).IsRequired();
        builder.Property(record => record.WinnerUserId).HasMaxLength(200).UseCollation(ExactCollation);

        builder.Property(record => record.Status).IsRequired();
        builder.Property(record => record.PlayerAScore).IsRequired();
        builder.Property(record => record.PlayerBScore).IsRequired();
        builder.Property(record => record.CurrentQuestionIndex).IsRequired();
        builder.Property(record => record.Draw).IsRequired();

        builder.Property(record => record.CreatedAt).HasConversion(UtcConverter.Instance).IsRequired();
        builder.Property(record => record.CompletedAt).HasConversion(UtcConverter.Instance);

        builder.Property(record => record.QuestionsJson).IsRequired();
        builder.Property(record => record.AnswersJson).IsRequired();

        builder.HasIndex(record => record.LobbyId).IsUnique().HasDatabaseName("UX_Matches_LobbyId");
        builder.HasIndex(record => new { record.PlayerAId, record.CreatedAt }).HasDatabaseName("IX_Matches_PlayerA_CreatedAt");
        builder.HasIndex(record => new { record.PlayerBId, record.CreatedAt }).HasDatabaseName("IX_Matches_PlayerB_CreatedAt");
    }
}
