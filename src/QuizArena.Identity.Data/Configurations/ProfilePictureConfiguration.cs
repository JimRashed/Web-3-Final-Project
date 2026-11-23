using System.Globalization;
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

    public const string ContentLengthConstraint = "CK_ProfilePictures_ContentLength";

    public const string ContentTypeConstraint = "CK_ProfilePictures_ContentType";

    public const string VersionConstraint = "CK_ProfilePictures_Version";

    public void Configure(EntityTypeBuilder<ProfilePicture> builder)
    {
        // The domain re-validates every read (ProfilePicture.Rehydrate/RehydrateInfo); these CHECK constraints make the
        // same invariants true at rest so a raw write can never store a row the application would refuse to read.
        builder.ToTable("ProfilePictures", table =>
        {
            table.HasCheckConstraint(ContentLengthConstraint, ContentLengthSql);
            table.HasCheckConstraint(ContentTypeConstraint, ContentTypeSql);
            table.HasCheckConstraint(VersionConstraint, "[Version] >= 1");
        });
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

    /// <summary>Stored bytes must be non-empty and at most <see cref="ProfilePicture.MaxContentLength"/>.</summary>
    private static string ContentLengthSql =>
        string.Create(CultureInfo.InvariantCulture, $"DATALENGTH([Content]) BETWEEN 1 AND {ProfilePicture.MaxContentLength}");

    /// <summary>
    /// Exactly one of <see cref="ProfilePicture.AllowedContentTypes"/>. A binary collation keeps the comparison
    /// case-sensitive like the domain's ordinal check (the database default collation is case-insensitive).
    /// The list is sorted so the generated model, and therefore migrations, stay deterministic.
    /// </summary>
    private static string ContentTypeSql =>
        "[ContentType] COLLATE Latin1_General_BIN2 IN ("
        + string.Join(", ", ProfilePicture.AllowedContentTypes.Order(StringComparer.Ordinal).Select(type => $"N'{type}'"))
        + ")";
}
