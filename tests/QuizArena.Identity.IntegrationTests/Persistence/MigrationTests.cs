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
    private const string PictureConstraintNames = "N'CK_ProfilePictures_ContentLength', N'CK_ProfilePictures_ContentType', N'CK_ProfilePictures_Version'";

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

    [Fact]
    public async Task AddProfilePictureCheckConstraints_PreservesValidPicturesUpAndDropsOnlyTheConstraintsDown()
    {
        // Arrange
        await using var context = CreateContext($"QuizArenaIdentity_PictureChecks_{Guid.NewGuid():N}");
        var migrator = context.GetService<IMigrator>();

        try
        {
            await migrator.MigrateAsync("AddProfilePictures");
            await SeedProfileWithPictureAsync(context, "image/png", [0x89, 0x50, 0x4E, 0x47], 3);
            var before = await PictureRowsAsync(context);

            // Act (up)
            await context.Database.MigrateAsync();
            var afterUp = await PictureRowsAsync(context);
            var trustedAfterUp = await ScalarAsync(context, $"SELECT COUNT(*) AS Value FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.ProfilePictures') AND is_not_trusted = 0 AND is_disabled = 0 AND name IN ({PictureConstraintNames})");

            // Act (down)
            await migrator.MigrateAsync("AddProfilePictures");
            var afterDown = await PictureRowsAsync(context);
            var constraintsAfterDown = await ScalarAsync(context, $"SELECT COUNT(*) AS Value FROM sys.check_constraints WHERE name IN ({PictureConstraintNames})");

            // Assert
            before.Should().ContainSingle();
            afterUp.Should().Equal(before);
            trustedAfterUp.Should().Be(3);
            afterDown.Should().Equal(before);
            constraintsAfterDown.Should().Be(0);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task AddProfilePictureCheckConstraints_WithExistingInvalidRow_FailsLoudlyAndAppliesNothing()
    {
        // Arrange: a row written before the constraints existed that violates one of them.
        await using var context = CreateContext($"QuizArenaIdentity_PictureChecksFail_{Guid.NewGuid():N}");
        var migrator = context.GetService<IMigrator>();

        try
        {
            await migrator.MigrateAsync("AddProfilePictures");
            await SeedProfileWithPictureAsync(context, "image/gif", [0x47, 0x49, 0x46], 1);

            // Act
            var migrate = () => context.Database.MigrateAsync();

            // Assert: SQL Server validates existing rows when a constraint is added, so the upgrade stops instead of
            // leaving untrusted constraints, and the migration's transaction rolls back every constraint it added.
            (await migrate.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
            (await context.Database.GetAppliedMigrationsAsync()).Should().NotContain(name => name.EndsWith("_AddProfilePictureCheckConstraints", StringComparison.Ordinal));
            (await ScalarAsync(context, $"SELECT COUNT(*) AS Value FROM sys.check_constraints WHERE name IN ({PictureConstraintNames})")).Should().Be(0);
            (await ScalarAsync(context, "SELECT COUNT(*) AS Value FROM dbo.ProfilePictures WHERE ContentType = N'image/gif'")).Should().Be(1);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task TightenProfilePictureContentTypeCheck_PreservesValidPicturesUpAndRestoresPreviousCheckDown()
    {
        // Arrange
        await using var context = CreateContext($"QuizArenaIdentity_PictureTypeCheck_{Guid.NewGuid():N}");
        var migrator = context.GetService<IMigrator>();

        try
        {
            await migrator.MigrateAsync("AddProfilePictureCheckConstraints");
            await SeedProfileWithPictureAsync(context, "image/jpeg", [0xFF, 0xD8, 0xFF], 1, '4');
            await SeedProfileWithPictureAsync(context, "image/png", [0x89, 0x50, 0x4E, 0x47], 2, '5');
            await SeedProfileWithPictureAsync(context, "image/webp", [0x52, 0x49, 0x46, 0x46], 3, '6');
            var before = await PictureRowsAsync(context);

            // Act (up)
            await context.Database.MigrateAsync();
            var afterUp = await PictureRowsAsync(context);
            var exactCheckAfterUp = await ScalarAsync(context, ContentTypeCheckCountSql(exact: true));

            // Act (down)
            await migrator.MigrateAsync("AddProfilePictureCheckConstraints");
            var afterDown = await PictureRowsAsync(context);
            var previousCheckAfterDown = await ScalarAsync(context, ContentTypeCheckCountSql(exact: false));

            // Assert
            before.Should().HaveCount(3);
            afterUp.Should().Equal(before);
            exactCheckAfterUp.Should().Be(1, "the sentinel-based exact check is trusted and enabled after up");
            afterDown.Should().Equal(before);
            previousCheckAfterDown.Should().Be(1, "down restores the previous IN-list check");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task TightenProfilePictureContentTypeCheck_WithExistingTrailingSpaceRow_FailsLoudlyAndKeepsPreviousCheck()
    {
        // Arrange: the previous check pads trailing spaces, so it admitted this row; the domain rejects it as corrupt.
        await using var context = CreateContext($"QuizArenaIdentity_PictureTypeCheckFail_{Guid.NewGuid():N}");
        var migrator = context.GetService<IMigrator>();

        try
        {
            await migrator.MigrateAsync("AddProfilePictureCheckConstraints");
            await SeedProfileWithPictureAsync(context, "image/png ", [0x89, 0x50, 0x4E, 0x47], 1);

            // Act
            var migrate = () => context.Database.MigrateAsync();

            // Assert: re-adding the constraint validates existing rows, and the transaction restores the old check.
            (await migrate.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
            (await context.Database.GetAppliedMigrationsAsync()).Should().NotContain(name => name.EndsWith("_TightenProfilePictureContentTypeCheck", StringComparison.Ordinal));
            (await ScalarAsync(context, ContentTypeCheckCountSql(exact: false))).Should().Be(1);
            (await ScalarAsync(context, "SELECT COUNT(*) AS Value FROM dbo.ProfilePictures WHERE DATALENGTH(ContentType) = DATALENGTH(N'image/png ')")).Should().Be(1);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>Counts the trusted, enabled content-type check in its exact (sentinel) or previous (padded IN) form.</summary>
    private static string ContentTypeCheckCountSql(bool exact) =>
        "SELECT COUNT(*) AS Value FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.ProfilePictures') AND name = N'CK_ProfilePictures_ContentType' AND is_not_trusted = 0 AND is_disabled = 0 AND definition "
        + (exact ? "LIKE" : "NOT LIKE") + " N'%image/png|%'";

    private static async Task SeedProfileWithPictureAsync(IdentityDbContext context, string contentType, byte[] content, int version, char idDigit = '3')
    {
        var profileId = Guid.Parse($"6f1c1e9e-0d7a-4b0e-9a57-{new string(idDigit, 12)}");
        var subject = $"subject-p{idDigit}";
        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO dbo.UserProfiles (Id, Issuer, Subject, DisplayName, CreatedAt, UpdatedAt) VALUES
            ({profileId}, N'http://localhost:8080/realms/quizarena', {subject}, N'Pic', '2026-11-01T10:00:00', '2026-11-01T10:00:00');
            INSERT INTO dbo.ProfilePictures (ProfileId, ContentType, Content, Version, UpdatedAt) VALUES
            ({profileId}, {contentType}, {content}, {version}, '2026-11-02T10:00:00.1234567');
            """);
    }

    private static async Task<List<string>> PictureRowsAsync(IdentityDbContext context) =>
        await context.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(CONVERT(nvarchar(36), ProfileId), N'|', ContentType, N'|', CONVERT(nvarchar(max), Content, 1), N'|', Version, N'|', CONVERT(nvarchar(33), UpdatedAt, 126), N'|', CONVERT(nvarchar(20), CONVERT(bigint, RowVersion))) AS Value FROM dbo.ProfilePictures ORDER BY ProfileId")
            .ToListAsync();

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
