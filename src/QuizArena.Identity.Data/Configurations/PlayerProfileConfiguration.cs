using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Identity.Domain.Entities;

namespace QuizArena.Identity.Data.Configurations;

public sealed class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
{
    /// <summary>Binary collation: issuer and subject compare exactly (case-sensitive, ordinal), as in the domain.</summary>
    public const string ExactCollation = "Latin1_General_100_BIN2";

    public void Configure(EntityTypeBuilder<PlayerProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).ValueGeneratedNever();

        builder.Property(profile => profile.Issuer)
            .HasMaxLength(PlayerProfile.MaxIssuerLength)
            .UseCollation(ExactCollation)
            .IsRequired();
        builder.Property(profile => profile.Subject)
            .HasMaxLength(PlayerProfile.MaxSubjectLength)
            .UseCollation(ExactCollation)
            .IsRequired();
        builder.Property(profile => profile.DisplayName)
            .HasMaxLength(PlayerProfile.MaxDisplayNameLength)
            .IsRequired();
        builder.Property(profile => profile.CreatedAt).HasConversion(UtcConverter.Instance).IsRequired();
        builder.Property(profile => profile.UpdatedAt).HasConversion(UtcConverter.Instance).IsRequired();

        builder.HasIndex(profile => new { profile.Issuer, profile.Subject })
            .IsUnique()
            .HasDatabaseName("UX_UserProfiles_Issuer_Subject");
    }
}
