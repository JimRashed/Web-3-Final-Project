using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Domain.Repositories;

namespace QuizArena.Gameplay.Api.Services.Outbox;

/// <summary>Sends one immutable payload to one destination. Implementations own only transport.</summary>
public interface IDeliveryChannel
{
    string Destination { get; }

    string Operation { get; }

    Task<DeliveryAttempt> SendAsync(string payloadJson, CancellationToken cancellationToken);
}

/// <summary>
/// Shared HTTP delivery channel: machine token, a hard per-call timeout, a bounded Retry-After and
/// one credential refresh when the receiver answers 401 (an expired token is not a permanent fault).
/// </summary>
public abstract class HttpDeliveryChannel(
    IHttpClientFactory clients,
    IMachineTokenSource tokens,
    IOptions<GameplayServiceOptions> options,
    ILogger logger) : IDeliveryChannel
{
    public const string HttpClientName = "outbox-delivery";

    private const string CredentialRejected = "machine_token_rejected";

    public abstract string Destination { get; }

    public abstract string Operation { get; }

    protected abstract string Route { get; }

    protected abstract string Scope { get; }

    /// <summary>Configured base address of this destination's API.</summary>
    protected abstract string BaseAddress(GameplayServiceOptions options);

    public async Task<DeliveryAttempt> SendAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var endpoint = ResolveEndpoint();
        if (endpoint is null)
        {
            return DeliveryAttempt.Transient($"{Destination} service address is not configured");
        }

        var attempt = await PostAsync(endpoint, payloadJson, cancellationToken);
        if (attempt.Error != CredentialRejected)
        {
            return attempt;
        }

        // The cached token may have expired between acquisition and use: drop it and try once more.
        // A second 401 is a configuration fault, not a transient error.
        tokens.Invalidate(Scope);
        var retry = await PostAsync(endpoint, payloadJson, cancellationToken);
        return retry.Error == CredentialRejected
            ? DeliveryAttempt.Permanent("machine token rejected by the receiver")
            : retry;
    }

    private Uri? ResolveEndpoint()
    {
        var baseAddress = BaseAddress(options.Value);
        return Uri.TryCreate(baseAddress, UriKind.Absolute, out var baseUri) ? new Uri(baseUri, Route) : null;
    }

    private async Task<DeliveryAttempt> PostAsync(Uri endpoint, string payloadJson, CancellationToken cancellationToken)
    {
        try
        {
            using var client = clients.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var token = await tokens.GetAsync(Scope, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Value.RequestTimeout);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (response.IsSuccessStatusCode)
            {
                return DeliveryAttempt.Delivered;
            }

            var status = (int)response.StatusCode;
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return DeliveryAttempt.Permanent(CredentialRejected);
            }

            if (response.StatusCode is System.Net.HttpStatusCode.RequestTimeout
                or System.Net.HttpStatusCode.TooManyRequests
                || status >= 500)
            {
                return DeliveryAttempt.Transient($"receiver temporarily unavailable (HTTP {status})", RetryAfterOf(response));
            }

            logger.LogWarning("{Destination} rejected a delivery permanently (HTTP {Status}).", Destination, status);
            return DeliveryAttempt.Permanent($"receiver rejected the payload (HTTP {status})");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DeliveryAttempt.Transient("delivery timed out");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "{Destination} delivery failed at the transport level.", Destination);
            return DeliveryAttempt.Transient($"transport failure ({exception.GetType().Name})");
        }
        catch (MachineTokenUnavailableException exception)
        {
            logger.LogError(exception, "No machine token is available for {Destination}.", Destination);
            return DeliveryAttempt.Transient("machine token unavailable");
        }
    }

    private static TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : null;
        }

        return null;
    }
}

/// <summary>Posts player results to Statistics (<c>POST /api/results</c>, scope <c>results.write</c>).</summary>
public sealed class StatisticsResultChannel(
    IHttpClientFactory clients,
    IMachineTokenSource tokens,
    IOptions<GameplayServiceOptions> options,
    ILogger<StatisticsResultChannel> logger)
    : HttpDeliveryChannel(clients, tokens, options, logger)
{
    public override string Destination => MatchDelivery.Statistics;

    public override string Operation => MatchDeliveries.ResultOperation;

    protected override string Route => "api/results";

    protected override string Scope => GameplayServiceOptions.Scopes.ResultsWrite;

    protected override string BaseAddress(GameplayServiceOptions options) => options.Statistics;
}

/// <summary>Posts lifecycle events to Notification (<c>POST /events</c>, scope <c>events.write</c>).</summary>
public sealed class NotificationEventChannel(
    IHttpClientFactory clients,
    IMachineTokenSource tokens,
    IOptions<GameplayServiceOptions> options,
    ILogger<NotificationEventChannel> logger)
    : HttpDeliveryChannel(clients, tokens, options, logger)
{
    public override string Destination => MatchDelivery.Notification;

    public override string Operation => MatchDeliveries.EventOperation;

    protected override string Route => "events";

    protected override string Scope => GameplayServiceOptions.Scopes.EventsWrite;

    protected override string BaseAddress(GameplayServiceOptions options) => options.Notification;
}
