using Microsoft.Extensions.Options;
using QuizArena.Gameplay.Data.Enums;
using QuizArena.Gameplay.Data.Outbox;
using QuizArena.Gameplay.Data.Records;

namespace QuizArena.Gameplay.Api.Services.Outbox;

/// <summary>Worker tuning; the defaults are the numbers in ADR-07.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 10;

    /// <summary>How long a claimed row is owned before another worker may take it over.</summary>
    public int LeaseSeconds { get; set; } = 30;

    public int PollSeconds { get; set; } = 2;

    public TimeSpan Lease => TimeSpan.FromSeconds(LeaseSeconds <= 0 ? 30 : LeaseSeconds);

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollSeconds <= 0 ? 2 : PollSeconds);
}

/// <summary>
/// Delivers committed outbox rows. Every row is independent: one destination failing never blocks the
/// other, a crash leaves an expiring lease that a restarted worker reclaims, and exhausted rows stay
/// visible as <see cref="DeliveryStatus.Failed"/> for an operator redrive.
/// </summary>
public sealed class DeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IDeliveryChannel> channels,
    IOptions<OutboxOptions> options,
    TimeProvider clock,
    ILogger<DeliveryWorker> logger) : BackgroundService
{
    private readonly Dictionary<string, IDeliveryChannel> _channels =
        channels.ToDictionary(channel => channel.Destination, StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delivered = 0;
            try
            {
                delivered = await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A database hiccup or a poisoned row must not kill the loop: log and poll again.
                logger.LogError(exception, "The delivery worker iteration failed.");
            }

            try
            {
                await Task.Delay(delivered > 0 ? TimeSpan.Zero : options.Value.PollInterval, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Claims and processes one batch. Returns how many rows were claimed (0 means idle).</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var now = clock.GetUtcNow().UtcDateTime;

        var claimed = await store.ClaimAsync(options.Value.BatchSize, options.Value.Lease, now, cancellationToken);

        foreach (var delivery in claimed)
        {
            await ProcessAsync(store, delivery, now, cancellationToken);
        }

        return claimed.Count;
    }

    private async Task ProcessAsync(IOutboxStore store, DeliveryRecord delivery, DateTime startedAt, CancellationToken cancellationToken)
    {
        if (!_channels.TryGetValue(delivery.Destination, out var channel))
        {
            await store.FailedAsync(delivery.Id, $"unknown destination '{delivery.Destination}'", cancellationToken);
            return;
        }

        var attempt = await channel.SendAsync(delivery.PayloadJson, cancellationToken);

        switch (attempt.Disposition)
        {
            case DeliveryDisposition.Delivered:
                await store.DeliveredAsync(delivery.Id, startedAt, cancellationToken);
                return;

            case DeliveryDisposition.Permanent:
                logger.LogError("Delivery {DeliveryId} to {Destination} failed permanently: {Error}",
                    delivery.Id, delivery.Destination, attempt.Error);
                await store.FailedAsync(delivery.Id, attempt.Error ?? "permanent failure", cancellationToken);
                return;

            default:
                if (delivery.AttemptCount >= DeliveryRetryPolicy.MaxAttempts)
                {
                    await store.FailedAsync(
                        delivery.Id,
                        $"retries exhausted after {delivery.AttemptCount} attempts: {attempt.Error}",
                        cancellationToken);
                    return;
                }

                var delay = DeliveryRetryPolicy.DelayBefore(
                    delivery.AttemptCount,
                    attempt.RetryAfter,
                    Random.Shared.NextDouble());

                await store.RescheduleAsync(
                    delivery.Id,
                    startedAt + delay,
                    attempt.Error ?? "transient failure",
                    cancellationToken);
                return;
        }
    }
}
