using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Identity.Domain.Entities;

namespace QuizArena.Identity.Data.Configurations;

/// <summary>
/// Maps <see cref="ProfilePicture"/> to the private <c>ProfilePictures</c> table (SEC-05): one row per profile,
/// deleted with the profile, guarded by a SQL Server rowversion for first-committer-wins replacement (SEC-07).
/// </summary>
public sealed class ProfilePictureConfiguration : IEntityTypeConfiguration<ProfilePicture>
{
    /// <summary>Shadow concurrency token; kept out of the domain model.</summary>
    public const string RowVersionProperty = "RowVersion";

    public void Configure(EntityTypeBuilder<ProfilePicture> builder)
    {
        builder.ToTable("ProfilePictures");
        builder.HasKey(picture => picture.ProfileId);
        builder.Property(picture => picture.ProfileId).ValueGeneratedNever();

        builder.HasOne<PlayerProfile>()
            .WithOne()
            .HasForeignKey<ProfilePicture>(picture => picture.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(picture => picture.ContentType)
            .HasMaxLength(ProfilePicture.MaxContentTypeLength)
            .IsRequired();
        builder.Property(picture => picture.Content)
            .HasColumnType("varbinary(max)")
            .IsRequired();
        builder.Property(picture => picture.Version).IsRequired();
        builder.Property(picture => picture.UpdatedAt).HasConversion(UtcConverter.Instance).IsRequired();
        builder.Property<byte[]>(RowVersionProperty).IsRowVersion();
    }
}
