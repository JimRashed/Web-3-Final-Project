using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.QuestionBank.Data.Entities;

namespace QuizArena.QuestionBank.Data.Configurations;

public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOptionRecord>
{
    public void Configure(EntityTypeBuilder<QuestionOptionRecord> builder)
    {
        builder.ToTable("QuestionOptions", table => table.HasCheckConstraint("CK_QuestionOptions_Position", "[Position] >= 0 AND [Position] < 4"));
        builder.HasKey(option => new { option.QuestionId, option.Position });
        builder.Property(option => option.Value).HasMaxLength(300).UseCollation("Latin1_General_100_BIN2").IsRequired();
        // Domain validation owns ordinal value uniqueness: SQL text indexes ignore trailing spaces.
    }
}
