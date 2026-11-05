using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace QuizArena.Identity.Data.Configurations;

/// <summary>SQL Server datetime2 has no kind; values are stored as UTC and read back marked as UTC.</summary>
internal sealed class UtcConverter : ValueConverter<DateTime, DateTime>
{
    public static readonly UtcConverter Instance = new();

    private UtcConverter()
        : base(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
