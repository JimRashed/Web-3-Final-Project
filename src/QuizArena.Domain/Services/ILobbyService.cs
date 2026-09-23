using QuizArena.Domain.Contracts;

namespace QuizArena.Domain.Services;

public interface ILobbyService
{
    Task<LobbyProvisionResponse?> LockAsync(string joinCode, CancellationToken cancellationToken = default);
}
