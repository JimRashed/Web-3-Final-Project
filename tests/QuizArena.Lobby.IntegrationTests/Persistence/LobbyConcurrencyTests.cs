using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuizArena.Lobby.Data.Repositories;
using QuizArena.Lobby.Domain.Entities;
using QuizArena.Lobby.Domain.Enums;
using QuizArena.Lobby.Domain.Exceptions;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Persistence;

public sealed class LobbyConcurrencyTests
{
    [Fact]
    public async Task Join_ConcurrentDistinctLastSlotRequests_OnlyOneCommitsAfterReload()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("LastSlot");
        var created = await database.Repository().CreateAsync("host");
        var barrier = new CompetingSaveBarrier();

        // Act
        var results = await Task.WhenAll(
            CaptureAsync(() => database.Repository(null, barrier).JoinAsync(created.JoinCode, "first")),
            CaptureAsync(() => database.Repository(null, barrier).JoinAsync(created.JoinCode, "second")));

        // Assert
        results.Count(result => result.Error is null).Should().Be(1);
        results.Count(result => result.Error is LobbyConflictException).Should().Be(1);
        var persisted = await database.Repository().GetByJoinCodeAsync(created.JoinCode);
        persisted!.Players.Should().HaveCount(2);
        persisted.Players.Should().ContainSingle(player => player.PlayerId == "host");
        persisted.Status.Should().Be(LobbyStatus.Open);
    }

    [Fact]
    public async Task Join_ConcurrentSameSubject_ConvergesToOneMembership()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("DuplicateRace");
        var created = await database.Repository().CreateAsync("host");
        var barrier = new CompetingSaveBarrier();

        // Act
        var results = await Task.WhenAll(
            database.Repository(null, barrier).JoinAsync(created.JoinCode, "guest"),
            database.Repository(null, barrier).JoinAsync(created.JoinCode, "guest"));

        // Assert
        results.Should().OnlyContain(lobby => lobby != null && lobby.Players.Count == 2);
        var guestIds = results.Select(lobby => lobby!.Players.Single(player => player.PlayerId == "guest").Id);
        // Both successful responses converge on the winner's existing member ID.
        guestIds.Distinct().Should().ContainSingle();
        await using var reader = database.CreateContext();
        (await reader.LobbyPlayers.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Join_OverlapsHostLockWithOneMember_LockConflictsBeforeJoinCommits()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("JoinLockOne");
        var created = await database.Repository().CreateAsync("host");
        var pause = new PausedSave();
        var joining = database.Repository(null, pause).JoinAsync(created.JoinCode, "guest");
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));

        try
        {
            // Act
            Func<Task> act = () => database.Repository().SaveAsync(created.JoinCode, lobby => lobby.Lock("host"));

            // Assert
            await act.Should().ThrowAsync<LobbyConflictException>();
        }
        finally
        {
            pause.Release.TrySetResult(true);
            await joining;
        }

        var persisted = await database.Repository().GetByJoinCodeAsync(created.JoinCode);
        persisted!.Status.Should().Be(LobbyStatus.Open);
        persisted.Players.Should().HaveCount(2);
    }

    [Fact]
    public async Task Lock_OverlapsExistingMemberJoin_LeavesValidLockedTwoMemberLobby()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("JoinLockFull");
        var created = await database.Repository().CreateAsync("host");
        await database.Repository().JoinAsync(created.JoinCode, "guest");
        var pause = new PausedSave();
        var locking = database.Repository(null, pause).SaveAsync(created.JoinCode, lobby => lobby.Lock("host"));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));

        try
        {
            // Act
            var duplicate = await database.Repository().JoinAsync(created.JoinCode, "guest");

            // Assert
            duplicate!.Status.Should().Be(LobbyStatus.Open);
            duplicate.Players.Should().HaveCount(2);
        }
        finally
        {
            pause.Release.TrySetResult(true);
            await locking;
        }

        var persisted = await database.Repository().GetByJoinCodeAsync(created.JoinCode);
        persisted!.Status.Should().Be(LobbyStatus.Locked);
        persisted.Players.Should().HaveCount(2);
        Func<Task> lateJoin = () => database.Repository().JoinAsync(created.JoinCode, "late");
        await lateJoin.Should().ThrowAsync<LobbyConflictException>();
    }

    [Fact]
    public async Task Save_ParentChangesEveryAttempt_StopsAfterThreeAndRollsBackAllMemberships()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("MutationExhaustion");
        var created = await database.Repository().CreateAsync("host");
        var conflict = new ForceVersionConflict(database);

        // Act
        Func<Task> act = () => database.Repository(null, conflict).JoinAsync(created.JoinCode, "guest");

        // Assert
        var failure = await act.Should().ThrowAsync<LobbyPersistenceException>();
        failure.Which.Message.Should().Be("The lobby changed repeatedly. Please retry.");
        conflict.Attempts.Should().Be(3);
        await using var reader = database.CreateContext();
        var persisted = await reader.ActiveLobbies.Include(lobby => lobby.Players).SingleAsync();
        persisted.WriteRevision.Should().Be(3);
        persisted.Players.Should().ContainSingle().Which.PlayerId.Should().Be("host");
    }

    [Fact]
    public async Task Create_CancelledWhileSaving_PropagatesWithoutRetryOrPartialCommit()
    {
        // Arrange
        await using var database = await LobbyDatabase.CreateAsync("CancelSaving");
        var codes = new SequenceCodeSource("ABC123");
        var pause = new PausedSave();
        using var cancellation = new CancellationTokenSource();
        var creating = database.Repository(codes, pause).CreateAsync("host", cancellation.Token);
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));

        // Act
        await cancellation.CancelAsync();

        // Assert
        Func<Task> act = () => creating;
        await act.Should().ThrowAsync<OperationCanceledException>();
        codes.Calls.Should().Be(1);
        await using var reader = database.CreateContext();
        (await reader.ActiveLobbies.CountAsync()).Should().Be(0);
        (await reader.LobbyPlayers.CountAsync()).Should().Be(0);
    }

    private static async Task<(ActiveLobby? Lobby, Exception? Error)> CaptureAsync(Func<Task<ActiveLobby?>> operation)
    {
        try
        {
            return (await operation(), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private sealed class CompetingSaveBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource<bool> _bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _bothRead.TrySetResult(true);
            }

            await _bothRead.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    private sealed class PausedSave : SaveChangesInterceptor
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    private sealed class ForceVersionConflict(LobbyDatabase database) : SaveChangesInterceptor
    {
        public int Attempts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Attempts++;
            await using var competing = database.CreateContext();
            await competing.ActiveLobbies.ExecuteUpdateAsync(update => update.SetProperty(lobby => lobby.WriteRevision, lobby => lobby.WriteRevision + 1), cancellationToken);
            return result;
        }
    }
}
