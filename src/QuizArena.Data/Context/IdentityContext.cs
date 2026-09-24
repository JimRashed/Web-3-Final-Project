using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Identity.Entities;

namespace QuizArena.Data.Context;

public class IdentityContext : DbContext
{
    public IdentityContext(DbContextOptions<IdentityContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(u => u.Username)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired();
        });
    }
}