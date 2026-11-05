using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using QuizArena.Gameplay.Api.Services;
using Xunit;

namespace QuizArena.Gameplay.UnitTests.Services;

/// <summary>The real token source against a Keycloak-shaped (RFC 6749, snake_case) token response.</summary>
public sealed class KeycloakMachineTokenSourceTests
{
    private static readonly MachineTokenOptions Settings = new()
    {
        Authority = "http://localhost:8080/realms/quizarena",
        MachineClientId = "quizarena-gameplay",
        MachineClientSecret = "test-secret",
    };

    [Fact]
    public async Task GetAsync_WithKeycloakResponse_ReturnsTokenAndSendsClientCredentials()
    {
        // Arrange
        var handler = new RecordingHandler("""{"access_token":"header.payload.signature","expires_in":300,"token_type":"Bearer","scope":"lobbies.verify"}""");
        var sut = new KeycloakMachineTokenSource(new SingleClientFactory(handler), Options.Create(Settings), TimeProvider.System);

        // Act
        var token = await sut.GetAsync("lobbies.verify", CancellationToken.None);
        var cached = await sut.GetAsync("lobbies.verify", CancellationToken.None);

        // Assert
        token.Should().Be("header.payload.signature");
        cached.Should().Be(token);
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.Should().Be("http://localhost:8080/realms/quizarena/protocol/openid-connect/token");
        handler.Requests[0].Form.Should().Contain("grant_type=client_credentials").And.Contain("client_id=quizarena-gameplay")
            .And.Contain("client_secret=test-secret").And.Contain("scope=lobbies.verify");
    }

    [Theory]
    [InlineData("""{"accessToken":"camel-case-is-not-oauth","expiresIn":300}""")]
    [InlineData("""{"access_token":"","expires_in":300}""")]
    [InlineData("""{"access_token":"token","expires_in":0}""")]
    public async Task GetAsync_WithoutUsableToken_ThrowsInsteadOfSendingAnEmptyBearer(string body)
    {
        // Arrange
        var sut = new KeycloakMachineTokenSource(new SingleClientFactory(new RecordingHandler(body)), Options.Create(Settings), TimeProvider.System);

        // Act
        var act = () => sut.GetAsync("lobbies.verify", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<MachineTokenUnavailableException>();
    }

    private sealed class RecordingHandler(string body) : HttpMessageHandler
    {
        public List<(string Uri, string Form)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.ToString(), await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
