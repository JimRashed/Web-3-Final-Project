using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Domain.Exceptions;
using QuizArena.Gameplay.UnitTests.Outbox;
using Xunit;

namespace QuizArena.Gameplay.UnitTests.Services;

public sealed class LobbyHttpGatewayTests
{
    private const string LobbyJson = """
        {
          "lobbyId": "8d1c6c4e-2f7a-4a3e-9a51-0b7f0f1f2a3b",
          "joinCode": "ABC123",
          "status": "Locked",
          "hostUserId": "player-a",
          "playerIds": ["player-a", "player-b"]
        }
        """;

    private static LobbyHttpGateway Sut(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        out StubHttpMessageHandler handler,
        out StubTokenSource tokens,
        string? lobbyUrl = "http://lobby.test")
    {
        handler = new StubHttpMessageHandler(respond);
        tokens = new StubTokenSource();
        var options = Microsoft.Extensions.Options.Options.Create(new GameplayServiceOptions { Lobby = lobbyUrl ?? string.Empty });
        return new LobbyHttpGateway(new HttpClient(handler), tokens, options, NullLogger<LobbyHttpGateway>.Instance);
    }

    [Fact]
    public async Task GetAsync_WhenTheLobbyExists_MapsTheProvisionResponse()
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, LobbyJson), out var handler, out var tokens);

        // Act
        var lobby = await sut.GetAsync("ABC123", CancellationToken.None);

        // Assert
        lobby.Should().NotBeNull();
        lobby!.LobbyId.Should().Be("8d1c6c4e-2f7a-4a3e-9a51-0b7f0f1f2a3b");
        lobby.HostSubject.Should().Be("player-a");
        lobby.Members.Should().Equal("player-a", "player-b");
        lobby.IsLocked.Should().BeTrue();
        handler.Requests.Single().RequestUri!.ToString().Should().Be("http://lobby.test/internal/lobbies/ABC123");
        tokens.Scopes.Should().Equal(GameplayServiceOptions.Scopes.LobbyVerify);
    }

    [Fact]
    public async Task GetAsync_WhenTheJoinCodeIsUnknown_ReturnsNullWithoutFailing()
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.NotFound), out _, out _);

        // Act
        var lobby = await sut.GetAsync("ZZZ999", CancellationToken.None);

        // Assert
        lobby.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenTheLobbyIsOpen_IsNotLocked()
    {
        // Arrange
        var open = LobbyJson.Replace("Locked", "Open", StringComparison.Ordinal);
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, open), out _, out _);

        // Act
        var lobby = await sut.GetAsync("ABC123", CancellationToken.None);

        // Assert
        lobby!.IsLocked.Should().BeFalse();
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task GetAsync_WhenTheServiceRejectsOrFails_FailsClosedWith503(int status)
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json((HttpStatusCode)status), out _, out _);

        // Act
        var act = () => sut.GetAsync("ABC123", CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task GetAsync_WhenTheAddressIsNotConfigured_FailsClosedWith503()
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, LobbyJson), out var handler, out _, lobbyUrl: string.Empty);

        // Act
        var act = () => sut.GetAsync("ABC123", CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(503);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_WhenTheTransportFails_FailsClosedWith503()
    {
        // Arrange
        var sut = Sut(_ => throw new HttpRequestException("connection refused"), out _, out _);

        // Act
        var act = () => sut.GetAsync("ABC123", CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task GetAsync_WhenTheJoinCodeIsBlank_Returns400WithoutCallingLobby()
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, LobbyJson), out var handler, out _);

        // Act
        var act = () => sut.GetAsync("  ", CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(400);
        handler.Requests.Should().BeEmpty();
    }
}
