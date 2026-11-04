using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Lobby.Domain.Enums;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Persistence;

/// <summary>
/// Applies the Lobby migration lineage to a fresh, uniquely named SQL Server database and drops it afterwards.
/// Set QUIZARENA_TEST_SQLSERVER to a server connection string (without a database) to use something other than LocalDB.
/// </summary>
public sealed class MigrationTests
{
    [Fact]
    public async Task Migrate_OnFreshDatabase_AppliesEveryMigrationAndLeavesNonePending()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Migration");
        await using var context = database.CreateContext();

        // Act
        var applied = await context.Database.GetAppliedMigrationsAsync();

        // Assert
        applied.Should().Equal(context.Database.GetMigrations());
        applied.Should().HaveCount(2);
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        context.Model.GetEntityTypes().Select(entity => entity.GetTableName()).Should().BeEquivalentTo("ActiveLobbies", "LobbyPlayers");
        var tables = await context.Database.SqlQueryRaw<string>("SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'").ToListAsync();
        tables.Should().BeEquivalentTo("ActiveLobbies", "LobbyPlayers", "__EFMigrationsHistory");
    }

    [Fact]
    public async Task Model_ComparedWithLatestMigration_HasNoUnmigratedChanges()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("ModelCheck");
        await using var context = database.CreateContext();

        // Act
        var pending = context.Database.HasPendingModelChanges();

        // Assert
        pending.Should().BeFalse("every model change needs a migration: dotnet ef migrations add <Name> --project src/QuizArena.Lobby.Data --startup-project src/QuizArena.Lobby.Api --context LobbyDbContext");
    }

    [Fact]
    public async Task Persistence_DisposedWriters_NewContextReadsCompleteLockedAggregate()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Restart");
        var created = await database.Repository(new SequenceCodeSource("ABC123")).CreateAsync("Host");
        await database.Repository().JoinAsync(created.JoinCode, "host");
        await database.Repository().SaveAsync(created.JoinCode, lobby => lobby.Lock("Host"));

        // Act
        var result = await database.Repository().GetByJoinCodeAsync(created.JoinCode);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.HostId.Should().Be("Host");
        result.Status.Should().Be(LobbyStatus.Locked);
        result.Players.Select(player => player.PlayerId).Should().BeEquivalentTo("Host", "host");
    }
}
