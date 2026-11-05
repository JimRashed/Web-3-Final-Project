using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Gameplay.Data.Enums;
using QuizArena.Gameplay.Data.Records;

namespace QuizArena.Gameplay.Data.Configurations;

public sealed class DeliveryConfiguration : IEntityTypeConfiguration<DeliveryRecord>
{
    public void Configure(EntityTypeBuilder<DeliveryRecord> builder)
    {
        builder.ToTable("Deliveries");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Destination).HasMaxLength(32).UseCollation(MatchConfiguration.ExactCollation).IsRequired();
        builder.Property(record => record.Operation).HasMaxLength(64).UseCollation(MatchConfiguration.ExactCollation).IsRequired();
        builder.Property(record => record.PayloadJson).IsRequired();
        builder.Property(record => record.IdempotencyKey).HasMaxLength(200).UseCollation(MatchConfiguration.ExactCollation).IsRequired();
        builder.Property(record => record.Status).IsRequired();
        builder.Property(record => record.AttemptCount).IsRequired();
        builder.Property(record => record.LastError).HasMaxLength(400);

        builder.Property(record => record.NextAttemptAt).HasConversion(UtcConverter.Instance).IsRequired();
        builder.Property(record => record.LeaseUntil).HasConversion(UtcConverter.Instance);
        builder.Property(record => record.CreatedAt).HasConversion(UtcConverter.Instance).IsRequired();
        builder.Property(record => record.DeliveredAt).HasConversion(UtcConverter.Instance);

        // The worker's only hot query: due pending rows, oldest first.
        builder.HasIndex(record => new { record.Status, record.NextAttemptAt })
            .HasDatabaseName("IX_Deliveries_Status_NextAttemptAt");

        // A replay of the same enqueue cannot create a second copy of the same message.
        builder.HasIndex(record => new { record.Destination, record.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_Deliveries_Destination_IdempotencyKey");

        builder.Property(record => record.Status).HasSentinel(DeliveryStatus.Pending);
    }
}
