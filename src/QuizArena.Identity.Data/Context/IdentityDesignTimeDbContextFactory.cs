using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuizArena.Identity.Data.Context;

/// <summary>
/// Used only by <c>dotnet ef</c>. Targets local development LocalDB unless
/// <see cref="ConnectionEnvironmentVariable"/> supplies another connection string.
/// </summary>
public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public const string ConnectionEnvironmentVariable = "QUIZARENA_IDENTITY_CONNECTION";
    public const string DevelopmentConnection = "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaIdentityDb;Trusted_Connection=True;TrustServerCertificate=True";

    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connection) ? DevelopmentConnection : connection)
            .Options;
        return new IdentityDbContext(options);
    }
}
