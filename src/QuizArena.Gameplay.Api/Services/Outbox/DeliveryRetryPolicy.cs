namespace QuizArena.Gameplay.Api.Services.Outbox;

/// <summary>
/// Bounded exponential backoff for a delivery: eight attempts total, delay
/// <c>min(300s, 2^(attempt-1))</c> plus up to one second of jitter, and a receiver's Retry-After
/// honored up to five minutes.
/// </summary>
public static class DeliveryRetryPolicy
{
    public const int MaxAttempts = 8;

    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Delay before the attempt that follows <paramref name="attemptNumber"/> (1-based).
    /// </summary>
    public static TimeSpan DelayBefore(int attemptNumber, TimeSpan? retryAfter = null, double jitterSeconds = 0)
    {
        var exponential = TimeSpan.FromSeconds(Math.Pow(2, Math.Clamp(attemptNumber - 1, 0, 8)));
        var delay = exponential < MaxDelay ? exponential : MaxDelay;

        if (retryAfter is { } requested && requested > TimeSpan.Zero)
        {
            delay = requested < MaxDelay ? requested : MaxDelay;
        }

        var jitter = Math.Clamp(jitterSeconds, 0, 1);
        return delay + TimeSpan.FromSeconds(jitter);
    }
}
