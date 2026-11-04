using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuizArena.Statistics.Data.Context;

/// <summary>
/// Used only by <c>dotnet ef</c>. Targets local development LocalDB unless
/// <see cref="ConnectionEnvironmentVariable"/> supplies another connection string.
/// </summary>
public sealed class StatisticsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<StatisticsDbContext>
{
    public const string ConnectionEnvironmentVariable = "QUIZARENA_STATISTICS_CONNECTION";
    public const string DevelopmentConnection = "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaStatisticsDb;Trusted_Connection=True;TrustServerCertificate=True";

    public StatisticsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var options = new DbContextOptionsBuilder<StatisticsDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connection) ? DevelopmentConnection : connection)
            .Options;
        return new StatisticsDbContext(options);
    }
}
