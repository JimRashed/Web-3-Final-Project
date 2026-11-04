using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Lobby.Data.Entities;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Persistence;

public sealed class LobbyConstraintTests
{
    [Fact]
    public async Task Schema_OwnedMappings_HaveRequiredConstraintsAndBinarySubjects()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Schema");
        await using var context = database.CreateContext();
        var lobby = context.Model.FindEntityType(typeof(ActiveLobbyRow))!;
        var player = context.Model.FindEntityType(typeof(LobbyPlayerRow))!;

        // Act
        var indexes = lobby.GetIndexes().Concat(player.GetIndexes()).Where(index => index.IsUnique).Select(index => index.GetDatabaseName());

        // Assert
        indexes.Should().BeEquivalentTo("IX_ActiveLobbies_JoinCode", "IX_LobbyPlayers_LobbyId_PlayerId");
        lobby.FindProperty(nameof(ActiveLobbyRow.JoinCode))!.GetMaxLength().Should().Be(6);
        lobby.FindProperty(nameof(ActiveLobbyRow.JoinCode))!.IsUnicode().Should().BeFalse();
        lobby.FindProperty(nameof(ActiveLobbyRow.HostId))!.IsNullable.Should().BeFalse();
        player.FindProperty(nameof(LobbyPlayerRow.PlayerId))!.IsNullable.Should().BeFalse();
        lobby.FindProperty(nameof(ActiveLobbyRow.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        player.GetForeignKeys().Should().ContainSingle().Which.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        var columns = await context.Database.SqlQueryRaw<string>("SELECT COLLATION_NAME AS [Value] FROM INFORMATION_SCHEMA.COLUMNS WHERE COLUMN_NAME IN ('HostId', 'PlayerId', 'JoinCode')").ToListAsync();
        columns.Should().HaveCount(3).And.OnlyContain(collation => collation == "Latin1_General_100_BIN2");
    }

    [Fact]
    public async Task Insert_DuplicateJoinCode_IsRejectedWithoutPartialHost()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("CodeUnique");
        await database.Repository(new SequenceCodeSource("ABC123")).CreateAsync("host");
        await using var context = database.CreateContext();
        context.ActiveLobbies.Add(Row("ABC123", "other"));

        // Act
        Func<Task> act = () => context.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        await using var reader = database.CreateContext();
        (await reader.ActiveLobbies.CountAsync()).Should().Be(1);
        (await reader.LobbyPlayers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Insert_DuplicateExactMemberInSameLobby_IsRejected()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("MemberUnique");
        var created = await database.Repository().CreateAsync("host");
        await using var context = database.CreateContext();
        context.LobbyPlayers.Add(new LobbyPlayerRow { Id = Guid.NewGuid(), LobbyId = created.Id, PlayerId = "host" });

        // Act
        Func<Task> act = () => context.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        await using var reader = database.CreateContext();
        (await reader.LobbyPlayers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Insert_SameSubjectInDifferentLobbies_IsAllowed()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("DistinctLobbies");
        await database.Repository(new SequenceCodeSource("AAAAAA")).CreateAsync("host");

        // Act
        await database.Repository(new SequenceCodeSource("BBBBBB")).CreateAsync("host");

        // Assert
        await using var reader = database.CreateContext();
        (await reader.LobbyPlayers.CountAsync(player => player.PlayerId == "host")).Should().Be(2);
    }

    [Fact]
    public async Task Insert_MissingParent_IsRejectedByForeignKey()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("ForeignKey");
        await using var context = database.CreateContext();
        context.LobbyPlayers.Add(new LobbyPlayerRow { Id = Guid.NewGuid(), LobbyId = Guid.NewGuid(), PlayerId = "guest" });

        // Act
        Func<Task> act = () => context.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Delete_Parent_CascadesMembersInDatabase()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Cascade");
        var created = await database.Repository().CreateAsync("host");
        await database.Repository().JoinAsync(created.JoinCode, "guest");
        await using var context = database.CreateContext();
        var row = await context.ActiveLobbies.SingleAsync();
        context.Remove(row);

        // Act
        await context.SaveChangesAsync();

        // Assert
        await using var reader = database.CreateContext();
        (await reader.LobbyPlayers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Save_StaleParentVersion_RejectsUpdateAndRollsBackMemberInsert()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("StaleVersion");
        var created = await database.Repository().CreateAsync("host");
        await using var stale = database.CreateContext();
        var row = await stale.ActiveLobbies.SingleAsync();
        var originalVersion = row.RowVersion.ToArray();
        await database.Repository().JoinAsync(created.JoinCode, "winner");
        row.WriteRevision++;
        stale.LobbyPlayers.Add(new LobbyPlayerRow { Id = Guid.NewGuid(), LobbyId = created.Id, PlayerId = "loser" });

        // Act
        Func<Task> act = () => stale.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using var reader = database.CreateContext();
        var current = await reader.ActiveLobbies.Include(lobby => lobby.Players).SingleAsync();
        current.RowVersion.Should().NotBeEmpty().And.NotEqual(originalVersion);
        current.Players.Select(player => player.PlayerId).Should().BeEquivalentTo("host", "winner");
    }

    internal static ActiveLobbyRow Row(string code, string host)
    {
        var id = Guid.NewGuid();
        return new ActiveLobbyRow
        {
            Id = id,
            JoinCode = code,
            HostId = host,
            Players = [new LobbyPlayerRow { Id = Guid.NewGuid(), LobbyId = id, PlayerId = host }],
        };
    }
}
