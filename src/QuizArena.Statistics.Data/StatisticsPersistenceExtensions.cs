using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Statistics.Data.Context;

namespace QuizArena.Statistics.Data;

public static class StatisticsPersistenceExtensions
{
    /// <summary>Registers <see cref="StatisticsDbContext"/> (scoped) against the service's own SQL Server database.</summary>
    public static IServiceCollection AddStatisticsPersistence(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for the Statistics database.");
        }

        services.AddDbContext<StatisticsDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
