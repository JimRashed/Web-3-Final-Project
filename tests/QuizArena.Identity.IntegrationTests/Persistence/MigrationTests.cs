using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

    [Fact]
    public async Task AddProfilePictures_PreservesExistingProfilesUpAndDown()
    {
        // Arrange
        await using var context = CreateContext($"QuizArenaIdentity_PictureMigration_{Guid.NewGuid():N}");
        var migrator = context.GetService<IMigrator>();

        try
        {
            await migrator.MigrateAsync("AddUserProfiles");
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO dbo.UserProfiles (Id, Issuer, Subject, DisplayName, CreatedAt, UpdatedAt) VALUES
                ('6f1c1e9e-0d7a-4b0e-9a57-111111111111', N'http://localhost:8080/realms/quizarena', N'subject-x', N'Lower', '2026-11-01T10:00:00.1234567', '2026-11-02T10:00:00.7654321'),
                ('6f1c1e9e-0d7a-4b0e-9a57-222222222222', N'http://localhost:8080/realms/quizarena', N'Subject-X', N'Upper', '2026-11-03T10:00:00', '2026-11-03T10:00:00');
                """);
            var before = await ProfileRowsAsync(context);

            // Act (up)
            await context.Database.MigrateAsync();
            var afterUp = await ProfileRowsAsync(context);
            var picturesAfterUp = await ScalarAsync(context, "SELECT COUNT(*) AS Value FROM dbo.ProfilePictures");
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO dbo.ProfilePictures (ProfileId, ContentType, Content, Version, UpdatedAt) VALUES ('6f1c1e9e-0d7a-4b0e-9a57-111111111111', N'image/png', 0x89504E47, 1, SYSUTCDATETIME());");

            // Act (down)
            await migrator.MigrateAsync("AddUserProfiles");
            var afterDown = await ProfileRowsAsync(context);

            // Assert
            before.Should().HaveCount(2);
            afterUp.Should().Equal(before);
            picturesAfterUp.Should().Be(0);
            afterDown.Should().Equal(before);
            (await ScalarAsync(context, "SELECT CASE WHEN OBJECT_ID(N'dbo.ProfilePictures') IS NULL THEN 0 ELSE 1 END AS Value")).Should().Be(0);
            (await ScalarAsync(context, "SELECT COUNT(*) AS Value FROM sys.indexes WHERE name = N'UX_UserProfiles_Issuer_Subject' AND is_unique = 1")).Should().Be(1);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<List<string>> ProfileRowsAsync(IdentityDbContext context) =>
        await context.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(CONVERT(nvarchar(36), Id), N'|', Issuer COLLATE DATABASE_DEFAULT, N'|', Subject COLLATE DATABASE_DEFAULT, N'|', DisplayName, N'|', CONVERT(nvarchar(33), CreatedAt, 126), N'|', CONVERT(nvarchar(33), UpdatedAt, 126)) AS Value FROM dbo.UserProfiles ORDER BY Id")
            .ToListAsync();

    private static async Task<int> ScalarAsync(IdentityDbContext context, string sql) =>
        await context.Database.SqlQueryRaw<int>(sql).SingleAsync();

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
