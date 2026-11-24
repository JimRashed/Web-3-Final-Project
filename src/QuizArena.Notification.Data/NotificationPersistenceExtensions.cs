using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Notification.Data.Context;

namespace QuizArena.Notification.Data;

public static class NotificationPersistenceExtensions
{
    /// <summary>Registers <see cref="NotificationDbContext"/> (scoped) against the service's own SQL Server database.</summary>
    public static IServiceCollection AddNotificationPersistence(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for the Notification database.");
        }

        if (!IsWellFormedSqlServerConnectionString(connectionString))
        {
            // Name the key only: SqlClient's parse error can quote part of the value, so it is not chained.
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not a valid SQL Server connection string for the Notification database.");
        }

        services.AddDbContext<NotificationDbContext>(options => options.UseSqlServer(connectionString));
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
