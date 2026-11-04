using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.QuestionBank.Data.Entities;

namespace QuizArena.QuestionBank.Data.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<QuestionRecord>
{
    public void Configure(EntityTypeBuilder<QuestionRecord> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.Prompt).HasMaxLength(1000).IsRequired();
        builder.Property(question => question.CorrectAnswer).HasMaxLength(300).IsRequired();
        builder.HasOne(question => question.Category).WithMany().HasForeignKey(question => question.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(question => question.Options).WithOne().HasForeignKey(option => option.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}
