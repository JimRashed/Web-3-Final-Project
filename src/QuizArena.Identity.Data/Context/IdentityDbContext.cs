using Microsoft.EntityFrameworkCore;
using QuizArena.Identity.Domain.Entities;

namespace QuizArena.Identity.Data.Context;

/// <summary>
/// Owns the Identity service's own SQL Server database (QuizArenaIdentityDb); no other service reads or writes it.
/// Entity sets and IEntityTypeConfiguration classes in this assembly are applied automatically.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<PlayerProfile> UserProfiles => Set<PlayerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
