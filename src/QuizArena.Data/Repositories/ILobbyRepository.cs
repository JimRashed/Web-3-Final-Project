using QuizArena.Domain.Lobby.Entities;

namespace QuizArena.Data.Repositories;

public interface ILobbyRepository
{
    Task<ActiveLobby?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default);
    Task SaveAsync(ActiveLobby lobby, CancellationToken cancellationToken = default);
}
