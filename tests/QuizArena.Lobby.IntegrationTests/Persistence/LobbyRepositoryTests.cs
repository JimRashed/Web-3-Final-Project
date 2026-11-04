using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuizArena.Lobby.Data.Entities;
using QuizArena.Lobby.Data.Repositories;
using QuizArena.Lobby.Domain.Enums;
using QuizArena.Lobby.Domain.Exceptions;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Persistence;

public sealed class LobbyRepositoryTests
{
    [Fact]
    public async Task Create_ValidHost_PersistsRootAndHostWithGeneratedVersion()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Create");
        var repository = database.Repository(new SequenceCodeSource("ABC123"));

        // Act
        var result = await repository.CreateAsync("issuer|Host");

        // Assert
        result.Status.Should().Be(LobbyStatus.Open);
        result.Players.Should().ContainSingle().Which.PlayerId.Should().Be("issuer|Host");
        await using var reader = database.CreateContext();
        var row = await reader.ActiveLobbies.Include(lobby => lobby.Players).SingleAsync();
        row.Id.Should().Be(result.Id);
        row.RowVersion.Should().HaveCount(8);
        row.Players.Should().ContainSingle().Which.Id.Should().Be(result.Players[0].Id);
    }

    [Fact]
    public async Task Get_ExistingLobby_UsesNoTrackingAndPreservesCaseDistinctSubjects()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Read");
        var created = await database.Repository().CreateAsync("Host");
        await database.Repository().JoinAsync(created.JoinCode, "host");
        var factory = database.Factory();
        var repository = new LobbyRepository(factory, new RandomJoinCodeSource());

        // Act
        var result = await repository.GetByJoinCodeAsync(created.JoinCode);

        // Assert
        factory.TrackedEntities.Should().Be(0);
        result!.HostId.Should().Be("Host");
        result.Players.Select(player => player.PlayerId).Should().BeEquivalentTo("Host", "host");
    }

    [Theory]
    [InlineData("get")]
    [InlineData("join")]
    [InlineData("save")]
    public async Task Lookup_MissingLobby_ReturnsNull(string operation)
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Missing");
        var repository = database.Repository();

        // Act
        var result = operation switch
        {
            "get" => await repository.GetByJoinCodeAsync("ABC123"),
            "join" => await repository.JoinAsync("ABC123", "guest"),
            _ => await repository.SaveAsync("ABC123", lobby => lobby.Lock("host")),
        };

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("get")]
    [InlineData("join")]
    [InlineData("save")]
    public async Task Lookup_InvalidCode_ThrowsBeforeQuery(string operation)
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("InvalidCode");
        var factory = database.Factory();
        var repository = new LobbyRepository(factory, new RandomJoinCodeSource());

        // Act
        Func<Task> act = async () =>
        {
            if (operation == "get")
            {
                await repository.GetByJoinCodeAsync("abc123");
            }
            else if (operation == "join")
            {
                await repository.JoinAsync("abc123", "guest");
            }
            else
            {
                await repository.SaveAsync("abc123", lobby => lobby.Lock("host"));
            }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidLobbyException>();
        factory.TrackedEntities.Should().Be(0);
    }

    [Fact]
    public async Task Join_ExistingOpenMember_PreservesMembershipAndParentVersion()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Idempotent");
        var created = await database.Repository().CreateAsync("host");
        await database.Repository().JoinAsync(created.JoinCode, "guest");
        await using var beforeContext = database.CreateContext();
        var before = await beforeContext.ActiveLobbies.AsNoTracking().Include(lobby => lobby.Players).SingleAsync();

        // Act
        var result = await database.Repository().JoinAsync(created.JoinCode, "guest");

        // Assert
        result!.Players.Select(player => player.Id).Should().BeEquivalentTo(before.Players.Select(player => player.Id));
        await using var reader = database.CreateContext();
        (await reader.ActiveLobbies.SingleAsync()).RowVersion.Should().Equal(before.RowVersion);
        (await reader.LobbyPlayers.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(false, "third")]
    [InlineData(true, "third")]
    [InlineData(true, "guest")]
    public async Task Join_FullOrLocked_ConflictsWithoutPersistedChange(bool locked, string subject)
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("RejectJoin");
        var created = await database.Repository().CreateAsync("host");
        await database.Repository().JoinAsync(created.JoinCode, "guest");
        if (locked)
        {
            await database.Repository().SaveAsync(created.JoinCode, lobby => lobby.Lock("host"));
        }

        // Act
        Func<Task> act = () => database.Repository().JoinAsync(created.JoinCode, subject);

        // Assert
        await act.Should().ThrowAsync<LobbyConflictException>();
        var persisted = await database.Repository().GetByJoinCodeAsync(created.JoinCode);
        persisted!.Players.Select(player => player.PlayerId).Should().BeEquivalentTo("host", "guest");
        persisted.Status.Should().Be(locked ? LobbyStatus.Locked : LobbyStatus.Open);
    }

    [Fact]
    public async Task Save_RepeatedHostLock_IsIdempotentAndNonhostRemainsForbidden()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Lock");
        var created = await database.Repository().CreateAsync("host");
        await database.Repository().JoinAsync(created.JoinCode, "guest");
        await using var before = database.CreateContext();
        var version = (await before.ActiveLobbies.AsNoTracking().SingleAsync()).RowVersion;
        await database.Repository().SaveAsync(created.JoinCode, lobby => lobby.Lock("host"));
        await using var lockedContext = database.CreateContext();
        var lockedVersion = (await lockedContext.ActiveLobbies.SingleAsync()).RowVersion;

        // Act
        var repeated = await database.Repository().SaveAsync(created.JoinCode, lobby => lobby.Lock("host"));

        // Assert
        repeated!.Status.Should().Be(LobbyStatus.Locked);
        lockedVersion.Should().NotEqual(version);
        await using var reader = database.CreateContext();
        (await reader.ActiveLobbies.SingleAsync()).RowVersion.Should().Equal(lockedVersion);
        Func<Task> nonhost = () => database.Repository().SaveAsync(created.JoinCode, lobby => lobby.Lock("guest"));
        await nonhost.Should().ThrowAsync<LobbyForbiddenException>();
    }

    [Fact]
    public async Task Save_HostAlone_CannotLockPersistedLobby()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("HostAlone");
        var created = await database.Repository().CreateAsync("host");

        // Act
        Func<Task> act = () => database.Repository().SaveAsync(created.JoinCode, lobby => lobby.Lock("host"));

        // Assert
        await act.Should().ThrowAsync<LobbyConflictException>();
        (await database.Repository().GetByJoinCodeAsync(created.JoinCode))!.Status.Should().Be(LobbyStatus.Open);
    }

    [Fact]
    public async Task Get_CorruptStoredState_RejectsCheckedRehydration()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Corrupt");
        var created = await database.Repository().CreateAsync("host");
        await using var writer = database.CreateContext();
        await writer.ActiveLobbies.ExecuteUpdateAsync(update => update.SetProperty(lobby => lobby.Status, (LobbyStatus)42));

        // Act
        Func<Task> act = () => database.Repository().GetByJoinCodeAsync(created.JoinCode);

        // Assert
        await act.Should().ThrowAsync<InvalidLobbyException>();
    }

    [Fact]
    public async Task Create_CollisionsThenFreeCode_RetriesWithoutPartialRows()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("Collision");
        await database.Repository(new SequenceCodeSource("ABC123")).CreateAsync("existing");
        var codes = new SequenceCodeSource("ABC123", "ABC123", "XYZ789");

        // Act
        var result = await database.Repository(codes).CreateAsync("new-host");

        // Assert
        result.JoinCode.Should().Be("XYZ789");
        codes.Calls.Should().Be(3);
        await using var reader = database.CreateContext();
        (await reader.ActiveLobbies.CountAsync()).Should().Be(2);
        (await reader.LobbyPlayers.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Create_AllCodesCollide_StopsAfterFiveWithSafeFailure()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("ExhaustCode");
        await database.Repository(new SequenceCodeSource("ABC123")).CreateAsync("existing");
        var codes = new SequenceCodeSource("ABC123");

        // Act
        Func<Task> act = () => database.Repository(codes).CreateAsync("new-host");

        // Assert
        var failure = await act.Should().ThrowAsync<LobbyPersistenceException>();
        failure.Which.Message.Should().Be("A lobby join code could not be allocated. Please retry.");
        codes.Calls.Should().Be(5);
        await using var reader = database.CreateContext();
        (await reader.ActiveLobbies.CountAsync()).Should().Be(1);
        (await reader.LobbyPlayers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_UnrelatedMembershipUniqueViolation_DoesNotRetry()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("UnrelatedUnique");
        var codes = new SequenceCodeSource("ABC123");

        // Act
        Func<Task> act = () => database.Repository(codes, new DuplicateMembershipInterceptor()).CreateAsync("host");

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        codes.Calls.Should().Be(1);
        await using var reader = database.CreateContext();
        (await reader.ActiveLobbies.CountAsync()).Should().Be(0);
        (await reader.LobbyPlayers.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("get")]
    [InlineData("join")]
    [InlineData("save")]
    public async Task Operations_CancelledAtEntry_DoNotWriteOrGenerateCandidates(string operation)
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("CancelEntry");
        var codes = new SequenceCodeSource("ABC123");
        var repository = database.Repository(codes);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> act = async () =>
        {
            if (operation == "create")
            {
                await repository.CreateAsync("host", cancellation.Token);
            }
            else if (operation == "get")
            {
                await repository.GetByJoinCodeAsync("ABC123", cancellation.Token);
            }
            else if (operation == "join")
            {
                await repository.JoinAsync("ABC123", "guest", cancellation.Token);
            }
            else
            {
                await repository.SaveAsync("ABC123", lobby => lobby.Lock("host"), cancellation.Token);
            }
        };

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        codes.Calls.Should().Be(0);
        await using var reader = database.CreateContext();
        (await reader.ActiveLobbies.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_InvalidCandidate_PropagatesValidationWithoutRetry()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("InvalidCandidate");
        var codes = new SequenceCodeSource("abc123");

        // Act
        Func<Task> act = () => database.Repository(codes).CreateAsync("host");

        // Assert
        await act.Should().ThrowAsync<InvalidLobbyException>();
        codes.Calls.Should().Be(1);
    }

    private sealed class DuplicateMembershipInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var row = eventData.Context!.ChangeTracker.Entries<ActiveLobbyRow>().Single().Entity;
            row.Players.Add(new LobbyPlayerRow { Id = Guid.NewGuid(), LobbyId = row.Id, PlayerId = row.HostId });
            return ValueTask.FromResult(result);
        }
    }
}
