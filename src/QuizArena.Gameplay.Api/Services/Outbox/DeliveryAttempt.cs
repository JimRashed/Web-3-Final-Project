namespace QuizArena.Gameplay.Api.Services.Outbox;

/// <summary>What the worker should do with one attempt's outcome.</summary>
public enum DeliveryDisposition
{
    /// <summary>The receiver acknowledged the payload.</summary>
    Delivered,

    /// <summary>Worth retrying later: network failure, timeout, 408, 429 or a 5xx.</summary>
    Transient,

    /// <summary>Retrying cannot help: rejected payload, unknown route or a rejected service credential.</summary>
    Permanent,
}

/// <summary>Outcome of one delivery attempt, with a safe error code (never the payload or a response body).</summary>
public sealed record DeliveryAttempt(DeliveryDisposition Disposition, string? Error = null, TimeSpan? RetryAfter = null)
{
    public static DeliveryAttempt Delivered { get; } = new(DeliveryDisposition.Delivered);

    public static DeliveryAttempt Transient(string error, TimeSpan? retryAfter = null) =>
        new(DeliveryDisposition.Transient, error, retryAfter);

    public static DeliveryAttempt Permanent(string error) => new(DeliveryDisposition.Permanent, error);
}
