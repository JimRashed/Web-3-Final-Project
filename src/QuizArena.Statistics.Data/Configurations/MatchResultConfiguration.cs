using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Statistics.Domain.Entities;

namespace QuizArena.Statistics.Data.Configurations;

public sealed class MatchResultConfiguration : IEntityTypeConfiguration<MatchResult>
{
    /// <summary>Binary collation: ids compare and sort exactly (case-sensitive, ordinal), as in the domain.</summary>
    public const string ExactCollation = "Latin1_General_100_BIN2";

    public void Configure(EntityTypeBuilder<MatchResult> builder)
    {
        builder.ToTable("MatchResults", table =>
            table.HasCheckConstraint("CK_MatchResults_Score_NonNegative", "[Score] >= 0"));

        // Surrogate key keeps inserts append-only; the business key is the unique index below.
        builder.Property<long>("Id").UseIdentityColumn();
        builder.HasKey("Id");

        builder.Property(result => result.Issuer)
            .HasMaxLength(MatchResult.MaxIssuerLength)
            .UseCollation(ExactCollation)
            .IsRequired();
        builder.Property(result => result.MatchId)
            .HasMaxLength(MatchResult.MaxMatchIdLength)
            .UseCollation(ExactCollation)
            .IsRequired();
        builder.Property(result => result.UserId)
            .HasMaxLength(MatchResult.MaxUserIdLength)
            .UseCollation(ExactCollation)
            .IsRequired();
        builder.Property(result => result.Score).IsRequired();
        builder.Property(result => result.Won).IsRequired();
        builder.Property(result => result.PlayedAt).HasConversion(UtcConverter.Instance).IsRequired();

        builder.HasIndex(result => new { result.Issuer, result.MatchId, result.UserId })
            .IsUnique()
            .HasDatabaseName("UX_MatchResults_Issuer_MatchId_UserId");
        builder.HasIndex(result => new { result.Issuer, result.UserId, result.PlayedAt })
            .HasDatabaseName("IX_MatchResults_Issuer_UserId_PlayedAt");
    }
}
