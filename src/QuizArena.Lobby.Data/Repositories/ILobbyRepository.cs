using QuizArena.Lobby.Domain.Entities;

namespace QuizArena.Lobby.Data.Repositories;

public interface ILobbyRepository
{
    Task<ActiveLobby> CreateAsync(string hostId, CancellationToken cancellationToken = default);
    Task<ActiveLobby?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default);
    Task<ActiveLobby?> JoinAsync(string joinCode, string playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads and reruns a pure synchronous Domain mutation after a concurrency conflict.
    /// The callback returns whether state changed and must have no external side effects.
    /// </summary>
    Task<ActiveLobby?> SaveAsync(string joinCode, Func<ActiveLobby, bool> mutation, CancellationToken cancellationToken = default);
}
