using Microsoft.Data.SqlClient;
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

        if (!IsWellFormedSqlServerConnectionString(connectionString))
        {
            // Name the key only: SqlClient's parse error can quote part of the value, so it is not chained.
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not a valid SQL Server connection string for the Identity database.");
        }

        services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        services.AddScoped<IProfilePictureStore, ProfilePictureStore>();
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
