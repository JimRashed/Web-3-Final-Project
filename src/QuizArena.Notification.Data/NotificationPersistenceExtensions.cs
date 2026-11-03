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

        services.AddDbContext<NotificationDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
