using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Identity.Entities;

namespace QuizArena.Data.Context;

public class IdentityContext : DbContext
{
    public IdentityContext(DbContextOptions<IdentityContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
}