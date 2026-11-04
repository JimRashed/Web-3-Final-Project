using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Identity.Data.Context;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Persistence;

/// <summary>
/// Applies the Identity migration lineage to a fresh, uniquely named SQL Server database and drops it afterwards.
/// Set QUIZARENA_TEST_SQLSERVER to a server connection string (without a database) to use something other than LocalDB.
/// </summary>
public sealed class MigrationTests
{
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public async Task Migrate_OnFreshDatabase_AppliesEveryMigrationAndLeavesNonePending()
    {
        // Arrange
        await using var context = CreateContext($"QuizArenaIdentity_Migration_{Guid.NewGuid():N}");

        try
        {
            // Act
            await context.Database.MigrateAsync();

            // Assert
            (await context.Database.GetAppliedMigrationsAsync()).Should().Equal(context.Database.GetMigrations());
            (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public void Model_ComparedWithLatestMigration_HasNoUnmigratedChanges()
    {
        // Arrange
        using var context = CreateContext("QuizArenaIdentity_ModelCheck");

        // Act
        var pending = context.Database.HasPendingModelChanges();

        // Assert
        pending.Should().BeFalse("every model change needs a migration: dotnet ef migrations add <Name> --project src/QuizArena.Identity.Data --startup-project src/QuizArena.Identity.Api --context IdentityDbContext");
    }

    private static IdentityDbContext CreateContext(string database)
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = database,
        };
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(builder.ConnectionString).Options;
        return new IdentityDbContext(options);
    }
}
