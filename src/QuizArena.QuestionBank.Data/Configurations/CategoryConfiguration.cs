using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.QuestionBank.Data.Entities;

namespace QuizArena.QuestionBank.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<CategoryRecord>
{
    public void Configure(EntityTypeBuilder<CategoryRecord> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(category => category.Name).IsUnique();
    }
}
