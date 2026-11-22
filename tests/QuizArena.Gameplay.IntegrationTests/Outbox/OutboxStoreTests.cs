using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Gameplay.Data.Context;
using QuizArena.Gameplay.Data.Enums;
using QuizArena.Gameplay.Data.Outbox;
using QuizArena.Gameplay.Data.Records;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Outbox;

/// <summary>
/// The outbox claim and completion SQL against a real SQL Server database (created and dropped per test).
/// Each store gets its own context, as two worker processes would.
/// </summary>
public sealed class OutboxStoreTests : IAsyncLifetime
{
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);
    private readonly DbContextOptions<GameplayDbContext> _options;
    private readonly List<GameplayDbContext> _contexts = [];

    public OutboxStoreTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaGameplay_Outbox_{Guid.NewGuid():N}",
        };
        _options = new DbContextOptionsBuilder<GameplayDbContext>().UseSqlServer(connection.ConnectionString).Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new GameplayDbContext(_options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var context in _contexts)
        {
            await context.DisposeAsync();
        }

        await using var cleanup = new GameplayDbContext(_options);
        await cleanup.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task ClaimAsync_TwoWorkers_NeverClaimTheSameRow()
    {
        // Arrange
        await SeedAsync(4);

        // Act
        var first = await NewStore().ClaimAsync(10, Lease, Now, CancellationToken.None);
        var second = await NewStore().ClaimAsync(10, Lease, Now, CancellationToken.None);

        // Assert
        first.Should().HaveCount(4);
        second.Should().BeEmpty();
        first.Should().OnlyContain(row => row.LeaseUntil == Now + Lease && row.AttemptCount == 1);
    }

    [Fact]
    public async Task ClaimAsync_AfterTheLeaseExpires_AnotherWorkerReclaimsTheRow()
    {
        // Arrange
        var id = (await SeedAsync(1))[0];
        await NewStore().ClaimAsync(10, Lease, Now, CancellationToken.None);

        // Act
        var stillLeased = await NewStore().ClaimAsync(10, Lease, Now + Lease - TimeSpan.FromSeconds(1), CancellationToken.None);
        var reclaimed = await NewStore().ClaimAsync(10, Lease, Now + Lease + TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        stillLeased.Should().BeEmpty();
        reclaimed.Should().ContainSingle(row => row.Id == id && row.AttemptCount == 2);
    }

    [Fact]
    public async Task CompletionWrites_FromAWorkerWhoseLeaseExpired_DoNotOverwriteTheNewOwner()
    {
        // Arrange: worker A claims, its lease expires, worker B reclaims the same row.
        var id = (await SeedAsync(1))[0];
        var workerA = NewStore();
        var leaseA = (await workerA.ClaimAsync(10, Lease, Now, CancellationToken.None)).Single().LeaseUntil!.Value;
        var workerB = NewStore();
        var laterNow = Now + Lease + TimeSpan.FromSeconds(5);
        var leaseB = (await workerB.ClaimAsync(10, Lease, laterNow, CancellationToken.None)).Single().LeaseUntil!.Value;

        // Act: A finishes late; then B finishes.
        var aRescheduled = await workerA.RescheduleAsync(id, leaseA, Now + TimeSpan.FromMinutes(5), "transient", CancellationToken.None);
        var aFailed = await workerA.FailedAsync(id, leaseA, "late failure", CancellationToken.None);
        var aDelivered = await workerA.DeliveredAsync(id, leaseA, Now, CancellationToken.None);
        var stateAfterA = await ReadAsync(id);
        var bDelivered = await workerB.DeliveredAsync(id, leaseB, laterNow, CancellationToken.None);

        // Assert
        (aRescheduled, aFailed, aDelivered).Should().Be((false, false, false));
        stateAfterA.Status.Should().Be(DeliveryStatus.Pending);
        stateAfterA.LeaseUntil.Should().Be(leaseB);
        stateAfterA.LastError.Should().BeNull();
        bDelivered.Should().BeTrue();
        var final = await ReadAsync(id);
        final.Status.Should().Be(DeliveryStatus.Delivered);
        final.LeaseUntil.Should().BeNull();
    }

    [Fact]
    public async Task CompletionWrites_WithTheCurrentLease_ApplyAndReleaseIt()
    {
        // Arrange
        var ids = await SeedAsync(2);
        var store = NewStore();
        var claimed = await store.ClaimAsync(10, Lease, Now, CancellationToken.None);
        var lease = claimed[0].LeaseUntil!.Value;

        // Act
        var rescheduled = await store.RescheduleAsync(ids[0], lease, Now + TimeSpan.FromSeconds(2), "transient failure (HTTP 503)", CancellationToken.None);
        var failed = await store.FailedAsync(ids[1], lease, "permanent failure (HTTP 400)", CancellationToken.None);

        // Assert
        rescheduled.Should().BeTrue();
        failed.Should().BeTrue();
        var first = await ReadAsync(ids[0]);
        first.Status.Should().Be(DeliveryStatus.Pending);
        first.LeaseUntil.Should().BeNull();
        first.NextAttemptAt.Should().Be(Now + TimeSpan.FromSeconds(2));
        (await ReadAsync(ids[1])).Status.Should().Be(DeliveryStatus.Failed);
    }

    private OutboxStore NewStore()
    {
        var context = new GameplayDbContext(_options);
        _contexts.Add(context);
        return new OutboxStore(context);
    }

    private async Task<Guid[]> SeedAsync(int count)
    {
        await using var context = new GameplayDbContext(_options);
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
        {
            context.Deliveries.Add(new DeliveryRecord
            {
                Id = id,
                Destination = "statistics",
                Operation = "results.record.v1",
                PayloadJson = "{}",
                IdempotencyKey = id.ToString(),
                Status = DeliveryStatus.Pending,
                NextAttemptAt = Now - TimeSpan.FromSeconds(1),
                CreatedAt = Now - TimeSpan.FromSeconds(1),
            });
        }

        await context.SaveChangesAsync();
        return ids;
    }

    private async Task<DeliveryRecord> ReadAsync(Guid id)
    {
        await using var context = new GameplayDbContext(_options);
        return await context.Deliveries.AsNoTracking().SingleAsync(row => row.Id == id);
    }
}
