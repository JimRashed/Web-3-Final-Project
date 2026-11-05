using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Notification.Domain.Entities;

namespace QuizArena.Notification.Data.Configurations;

public sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("UserPreferences");

        // The identity pair is the natural key; there is exactly one row per player.
        builder.HasKey(preference => new { preference.Issuer, preference.Subject });
        builder.Property(preference => preference.Issuer)
            .HasMaxLength(UserPreference.MaxIssuerLength)
            .UseCollation(EventConfiguration.ExactCollation)
            .IsRequired();
        builder.Property(preference => preference.Subject)
            .HasMaxLength(UserPreference.MaxSubjectLength)
            .UseCollation(EventConfiguration.ExactCollation)
            .IsRequired();

        builder.Property(preference => preference.EmailEnabled).IsRequired();
        builder.Property(preference => preference.PushEnabled).IsRequired();
    }
}
