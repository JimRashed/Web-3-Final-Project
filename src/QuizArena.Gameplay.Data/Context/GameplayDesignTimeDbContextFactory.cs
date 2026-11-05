using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuizArena.Gameplay.Data.Context;

/// <summary>
/// Used only by <c>dotnet ef</c>. Targets local development LocalDB unless
/// <see cref="ConnectionEnvironmentVariable"/> supplies another connection string.
/// </summary>
public sealed class GameplayDesignTimeDbContextFactory : IDesignTimeDbContextFactory<GameplayDbContext>
{
    public const string ConnectionEnvironmentVariable = "QUIZARENA_GAMEPLAY_CONNECTION";
    public const string DevelopmentConnection = "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaGameplayDb;Trusted_Connection=True;TrustServerCertificate=True";

    public GameplayDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var options = new DbContextOptionsBuilder<GameplayDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connection) ? DevelopmentConnection : connection)
            .Options;
        return new GameplayDbContext(options);
    }
}
