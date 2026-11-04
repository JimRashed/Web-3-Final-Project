using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Identity.Data.Context;
using QuizArena.Identity.Data.Repositories;
using QuizArena.Identity.Domain.Repositories;

namespace QuizArena.Identity.Data;

public static class IdentityPersistenceExtensions
{
    /// <summary>Registers <see cref="IdentityDbContext"/> (scoped) against the service's own SQL Server database.</summary>
    public static IServiceCollection AddIdentityPersistence(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for the Identity database.");
        }

        services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        return services;
    }
}
