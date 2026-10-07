using FluentAssertions;
using Moq;
using QuizArena.Api.Services;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Lobby.Entities;

namespace QuizArena.UnitTests.Services;

public sealed class LobbyServiceTests
{
    [Fact]
    public async Task LockAsync_locks_lobby_persists_it_and_maps_players()
    {
        var lobby = new ActiveLobby { Id = Guid.NewGuid(), JoinCode = "ABC", HostId = "host", Players = new List<LobbyPlayer> { new() { PlayerId = "host" }, new() { PlayerId = "guest" } } };
        var repository = new Mock<ILobbyRepository>();
        repository.Setup(x => x.GetByJoinCodeAsync("ABC", It.IsAny<CancellationToken>())).ReturnsAsync(lobby);

        var result = await new LobbyService(repository.Object).LockAsync("ABC");

        result.Should().BeEquivalentTo(new LobbyProvisionResponse(lobby.Id, "ABC", "Locked", new[] { "host", "guest" }));
        repository.Verify(x => x.SaveAsync(lobby, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LockAsync_returns_null_for_unknown_code()
    {
        var repository = new Mock<ILobbyRepository>();
        repository.Setup(x => x.GetByJoinCodeAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((ActiveLobby?)null);

        (await new LobbyService(repository.Object).LockAsync("missing")).Should().BeNull();
        repository.Verify(x => x.SaveAsync(It.IsAny<ActiveLobby>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LockAsync_forwards_cancellation_and_propagates_repository_failure()
    {
        var cancellation = new CancellationTokenSource().Token;
        var repository = new Mock<ILobbyRepository>();
        repository.Setup(x => x.GetByJoinCodeAsync("ABC", cancellation))
            .ThrowsAsync(new InvalidOperationException("lobby store unavailable"));

        var action = () => new LobbyService(repository.Object).LockAsync("ABC", cancellation);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("lobby store unavailable");
        repository.Verify(x => x.GetByJoinCodeAsync("ABC", cancellation), Times.Once);
    }
}
