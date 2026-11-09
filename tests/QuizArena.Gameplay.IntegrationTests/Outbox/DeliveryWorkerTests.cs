using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Api.Services.Outbox;
using QuizArena.Gameplay.Data.Enums;
using QuizArena.Gameplay.Data.Outbox;
using QuizArena.Gameplay.Data.Records;
using QuizArena.Gameplay.Domain.Repositories;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Outbox;

/// <summary>
/// Worker decisions with an in-memory store and a scripted channel: no database, no network.
/// </summary>
public sealed class DeliveryWorkerTests
{
    private static DeliveryRecord Row(Guid id, int attempt, string destination = MatchDelivery.Statistics) => new()
    {
        Id = id,
        Destination = destination,
        Operation = MatchDeliveries.ResultOperation,
        PayloadJson = "{}",
        IdempotencyKey = $"key-{id}",
        Status = DeliveryStatus.Pending,
        AttemptCount = attempt,
        NextAttemptAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };

    private static DeliveryWorker Worker(FakeStore store, params IDeliveryChannel[] channels)
    {
        var services = new ServiceCollection();
        services.AddScoped<IOutboxStore>(_ => store);
        var provider = services.BuildServiceProvider();

        return new DeliveryWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            channels,
            Options.Create(new OutboxOptions()),
            TimeProvider.System,
            NullLogger<DeliveryWorker>.Instance);
    }

    [Fact]
    public async Task RunOnceAsync_WhenTheReceiverAccepts_MarksTheRowDelivered()
    {
        // Arrange
        var id = Guid.NewGuid();
        var store = new FakeStore(Row(id, attempt: 1));
        var worker = Worker(store, new ScriptedChannel(DeliveryAttempt.Delivered));

        // Act
        var claimed = await worker.RunOnceAsync(CancellationToken.None);

        // Assert
        claimed.Should().Be(1);
        store.Delivered.Should().Equal(id);
        store.Rescheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOnceAsync_WhenTheFailureIsTransient_ReschedulesWithABackoff()
    {
        // Arrange
        var id = Guid.NewGuid();
        var store = new FakeStore(Row(id, attempt: 3));
        var worker = Worker(store, new ScriptedChannel(DeliveryAttempt.Transient("HTTP 503")));

        // Act
        await worker.RunOnceAsync(CancellationToken.None);

        // Assert
        store.Rescheduled.Should().ContainSingle();
        var (rescheduledId, nextAttemptAt, error) = store.Rescheduled.Single();
        rescheduledId.Should().Be(id);
        nextAttemptAt.Should().BeCloseTo(DateTime.UtcNow + TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(2));
        error.Should().Be("HTTP 503");
    }

    [Fact]
    public async Task RunOnceAsync_WhenRetriesAreExhausted_MarksTheRowFailed()
    {
        // Arrange
        var id = Guid.NewGuid();
        var store = new FakeStore(Row(id, attempt: DeliveryRetryPolicy.MaxAttempts));
        var worker = Worker(store, new ScriptedChannel(DeliveryAttempt.Transient("HTTP 503")));

        // Act
        await worker.RunOnceAsync(CancellationToken.None);

        // Assert
        store.Failed.Should().ContainSingle().Which.Id.Should().Be(id);
        store.Rescheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOnceAsync_WhenTheFailureIsPermanent_MarksTheRowFailedOnce()
    {
        // Arrange
        var id = Guid.NewGuid();
        var store = new FakeStore(Row(id, attempt: 1));
        var worker = Worker(store, new ScriptedChannel(DeliveryAttempt.Permanent("HTTP 400")));

        // Act
        await worker.RunOnceAsync(CancellationToken.None);

        // Assert
        store.Failed.Should().ContainSingle().Which.Id.Should().Be(id);
    }

    [Fact]
    public async Task RunOnceAsync_WhenNoChannelMatchesTheDestination_FailsTheRowInsteadOfDroppingIt()
    {
        // Arrange
        var id = Guid.NewGuid();
        var store = new FakeStore(Row(id, attempt: 1, destination: "unknown"));
        var worker = Worker(store, new ScriptedChannel(DeliveryAttempt.Delivered));

        // Act
        await worker.RunOnceAsync(CancellationToken.None);

        // Assert
        store.Failed.Should().ContainSingle().Which.Reason.Should().Contain("unknown destination");
    }

    private sealed class FakeStore(params DeliveryRecord[] rows) : IOutboxStore
    {
        public List<Guid> Delivered { get; } = [];

        public List<(Guid Id, DateTime NextAttemptAt, string Error)> Rescheduled { get; } = [];

        public List<(Guid Id, string Reason)> Failed { get; } = [];

        // Like the real store, a claimed row always carries the lease the worker must present to complete it.
        public Task<IReadOnlyList<DeliveryRecord>> ClaimAsync(int batchSize, TimeSpan lease, DateTime utcNow, CancellationToken cancellationToken)
        {
            var claimed = rows.Take(batchSize).ToArray();
            foreach (var row in claimed)
            {
                row.LeaseUntil = utcNow + lease;
            }

            return Task.FromResult<IReadOnlyList<DeliveryRecord>>(claimed);
        }

        public List<DateTime> Leases { get; } = [];

        public Task<bool> DeliveredAsync(Guid id, DateTime claimedLeaseUntil, DateTime utcNow, CancellationToken cancellationToken)
        {
            Delivered.Add(id);
            Leases.Add(claimedLeaseUntil);
            return Task.FromResult(true);
        }

        public Task<bool> RescheduleAsync(Guid id, DateTime claimedLeaseUntil, DateTime nextAttemptAtUtc, string error, CancellationToken cancellationToken)
        {
            Rescheduled.Add((id, nextAttemptAtUtc, error));
            Leases.Add(claimedLeaseUntil);
            return Task.FromResult(true);
        }

        public Task<bool> FailedAsync(Guid id, DateTime claimedLeaseUntil, string error, CancellationToken cancellationToken)
        {
            Failed.Add((id, error));
            Leases.Add(claimedLeaseUntil);
            return Task.FromResult(true);
        }

        public Task<bool> RedriveAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class ScriptedChannel(DeliveryAttempt attempt) : IDeliveryChannel
    {
        public string Destination => MatchDelivery.Statistics;

        public string Operation => MatchDeliveries.ResultOperation;

        public Task<DeliveryAttempt> SendAsync(string payloadJson, CancellationToken cancellationToken) =>
            Task.FromResult(attempt);
    }
}
