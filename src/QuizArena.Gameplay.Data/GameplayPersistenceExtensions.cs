using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Gameplay.Data.Context;

namespace QuizArena.Gameplay.Data;

public static class GameplayPersistenceExtensions
{
    /// <summary>Registers <see cref="GameplayDbContext"/> (scoped) against the service's own SQL Server database.</summary>
    public static IServiceCollection AddGameplayPersistence(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for the Gameplay database.");
        }

        services.AddDbContext<GameplayDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
