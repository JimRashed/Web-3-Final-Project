using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Notification.Domain.Entities;

namespace QuizArena.Notification.Data.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    /// <summary>Binary collation: issuer, recipient and match ids compare and sort exactly, as in the domain.</summary>
    public const string ExactCollation = "Latin1_General_100_BIN2";

    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");

        // The producer-generated id is both the primary key and the idempotency key.
        builder.HasKey(@event => @event.EventId);
        builder.Property(@event => @event.EventId).ValueGeneratedNever();

        builder.Property(@event => @event.Issuer)
            .HasMaxLength(Event.MaxIssuerLength)
            .UseCollation(ExactCollation)
            .IsRequired();

        builder.Property(@event => @event.Type).IsRequired();

        builder.Property(@event => @event.UserId)
            .HasMaxLength(Event.MaxSubjectLength)
            .UseCollation(ExactCollation)
            .IsRequired();

        builder.Property(@event => @event.MatchId)
            .HasMaxLength(Event.MaxMatchIdLength)
            .UseCollation(ExactCollation)
            .IsRequired();

        builder.Property(@event => @event.WinnerUserId)
            .HasMaxLength(Event.MaxSubjectLength)
            .UseCollation(ExactCollation);

        builder.Property(@event => @event.Draw).IsRequired();
        builder.Property(@event => @event.CreatedAt).HasConversion(UtcConverter.Instance).IsRequired();

        builder.HasIndex(@event => new { @event.Issuer, @event.UserId, @event.CreatedAt })
            .HasDatabaseName("IX_Events_Issuer_UserId_CreatedAt");
    }
}
