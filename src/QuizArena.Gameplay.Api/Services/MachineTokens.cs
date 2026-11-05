using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace QuizArena.Gameplay.Api.Services;

/// <summary>
/// Supplies short-lived machine access tokens for inter-service calls. Tokens are cached per scope
/// until shortly before expiry, and a rejected token can be dropped so the next call re-acquires one.
/// </summary>
public interface IMachineTokenSource
{
    Task<string> GetAsync(string scope, CancellationToken cancellationToken);

    /// <summary>Forces the next <see cref="GetAsync"/> to start from the identity provider again.</summary>
    void Invalidate(string scope);
}

/// <summary>Configuration for the confidential Gameplay client (Keycloak client-credentials grant).</summary>
public sealed class MachineTokenOptions
{
    public const string SectionName = "Authentication";

    public const string HttpClientName = "machine-token";

    public string Authority { get; set; } = string.Empty;

    /// <summary>Bound from <c>Authentication:MachineClientId</c>, the same key the API's token validation uses.</summary>
    public string MachineClientId { get; set; } = string.Empty;

    /// <summary>Bound from <c>Authentication:MachineClientSecret</c>; supplied only through the environment.</summary>
    public string MachineClientSecret { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 5;
}

/// <summary>Raised when no machine token can be obtained; adapters turn this into 503, never 500.</summary>
public sealed class MachineTokenUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Keycloak client-credentials client. Requests one scope at a time: the realm maps each scope to the
/// audience of the service that consumes it, so a token for <c>results.write</c> carries only the
/// Statistics audience.
/// </summary>
public sealed class KeycloakMachineTokenSource(
    IHttpClientFactory httpClientFactory,
    IOptions<MachineTokenOptions> options,
    TimeProvider clock) : IMachineTokenSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, CachedToken> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string> GetAsync(string scope, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(scope, out var cached) && cached.ExpiresAt > clock.GetUtcNow())
        {
            return cached.Token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(scope, out cached) && cached.ExpiresAt > clock.GetUtcNow())
            {
                return cached.Token;
            }

            var settings = options.Value;
            if (string.IsNullOrWhiteSpace(settings.Authority) || string.IsNullOrWhiteSpace(settings.MachineClientId) || string.IsNullOrWhiteSpace(settings.MachineClientSecret))
            {
                throw new MachineTokenUnavailableException(
                    "The machine client is not configured (Authentication:Authority, Authentication:MachineClientId and Authentication:MachineClientSecret are required).");
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(new Uri(settings.Authority.TrimEnd('/') + "/"), "protocol/openid-connect/token"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = settings.MachineClientId,
                    ["client_secret"] = settings.MachineClientSecret,
                    ["scope"] = scope,
                }),
            };

            using var client = httpClientFactory.CreateClient(MachineTokenOptions.HttpClientName);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds <= 0 ? 5 : settings.RequestTimeoutSeconds));

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                // Never surface the identity provider's body: it may echo request parameters.
                throw new MachineTokenUnavailableException(
                    $"The identity provider refused the machine token request (HTTP {(int)response.StatusCode}).");
            }

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, timeout.Token);
            if (string.IsNullOrWhiteSpace(payload?.AccessToken) || payload.ExpiresIn <= 0)
            {
                throw new MachineTokenUnavailableException("The identity provider returned no usable access token.");
            }

            var lifetime = TimeSpan.FromSeconds(payload.ExpiresIn);
            var refreshAt = clock.GetUtcNow() + (lifetime > TimeSpan.FromSeconds(60) ? lifetime - TimeSpan.FromSeconds(30) : lifetime);

            _cache[scope] = new CachedToken(payload.AccessToken, refreshAt);
            return payload.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate(string scope) => _cache.TryRemove(scope, out _);

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);

    // OAuth token responses use snake_case (RFC 6749 section 5.1), not the web camelCase default.
    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>Applies a machine bearer token to an outgoing request.</summary>
public static class MachineTokenHttp
{
    public static async Task<HttpRequestMessage> WithMachineTokenAsync(
        this IMachineTokenSource tokens,
        HttpRequestMessage request,
        string scope,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(scope, cancellationToken));
        return request;
    }
}
