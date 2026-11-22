using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Lobby.Data.Context;
using QuizArena.Lobby.Data.Repositories;

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

        if (!IsWellFormedSqlServerConnectionString(connectionString))
        {
            // Name the key only: SqlClient's parse error can quote part of the value, so it is not chained.
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not a valid SQL Server connection string for the Lobby database.");
        }

        services.AddDbContextFactory<LobbyDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ILobbyRepository, LobbyRepository>();
        services.AddSingleton<IJoinCodeSource, RandomJoinCodeSource>();
        return services;
    }

    /// <summary>Parses the connection string without opening a connection.</summary>
    private static bool IsWellFormedSqlServerConnectionString(string connectionString)
    {
        try
        {
            _ = new SqlConnectionStringBuilder(connectionString);
            return true;
        }
        catch (Exception parseFailure) when (parseFailure is ArgumentException or FormatException or OverflowException)
        {
            return false;
        }
    }
}
