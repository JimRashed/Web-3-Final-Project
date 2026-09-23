using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Services;

public sealed class LobbyService(ILobbyRepository lobbyRepository) : ILobbyService
{
    public async Task<LobbyProvisionResponse?> LockAsync(string joinCode, CancellationToken cancellationToken = default)
    {
        var lobby = await lobbyRepository.GetByJoinCodeAsync(joinCode, cancellationToken);
        if (lobby is null)
        {
            return null;
        }

        lobby.Status = "Locked";
        await lobbyRepository.SaveAsync(lobby, cancellationToken);

        return new LobbyProvisionResponse(
            lobby.Id,
            lobby.JoinCode,
            lobby.Status,
            lobby.Players.Select(player => player.PlayerId).ToList());
    }
}
