using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using QuizArena.Gameplay.Api.Services;

namespace QuizArena.Gameplay.UnitTests.Outbox;

/// <summary>Test doubles for the outbound HTTP tests: no network, no identity provider.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Request bodies captured at send time (the adapter disposes the request afterwards).</summary>
    public List<string?> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        return respond(request);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string body = "{}") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

/// <summary>Mimics IHttpClientFactory: every call returns a fresh client over the same handler.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

internal sealed class StubTokenSource : IMachineTokenSource
{
    public List<string> Scopes { get; } = [];

    public List<string> Invalidated { get; } = [];

    public string Token { get; set; } = "machine-token";

    public Task<string> GetAsync(string scope, CancellationToken cancellationToken)
    {
        Scopes.Add(scope);
        return Task.FromResult(Token);
    }

    public void Invalidate(string scope) => Invalidated.Add(scope);
}

internal static class TestOptions
{
    public static IOptions<GameplayServiceOptions> Services(
        string? statistics = "http://statistics.test",
        string? notification = "http://notification.test") =>
        Options.Create(new GameplayServiceOptions
        {
            Lobby = "http://lobby.test",
            QuestionBank = "http://questionbank.test",
            Statistics = statistics ?? string.Empty,
            Notification = notification ?? string.Empty,
        });
}
