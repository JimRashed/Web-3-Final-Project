using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Statistics.Data.Context;
using QuizArena.Statistics.Data.Repositories;
using QuizArena.Statistics.Domain.Repositories;

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

        if (!IsWellFormedSqlServerConnectionString(connectionString))
        {
            // Name the key only: SqlClient's parse error can quote part of the value, so it is not chained.
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not a valid SQL Server connection string for the Statistics database.");
        }

        services.AddDbContext<StatisticsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IMatchResultRepository, MatchResultRepository>();
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
