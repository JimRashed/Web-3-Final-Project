using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Lobby.Data.Context;

namespace QuizArena.Lobby.Data;

public static class LobbyPersistenceExtensions
{
    /// <summary>Registers <see cref="LobbyDbContext"/> (scoped) against the service's own SQL Server database.</summary>
    public static IServiceCollection AddLobbyPersistence(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for the Lobby database.");
        }

        services.AddDbContext<LobbyDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
