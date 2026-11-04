using System.Security.Claims;
using QuizArena.Lobby.Api.Contracts;

namespace QuizArena.Lobby.Api.Services;

public interface ILobbyService
{
    Task<LobbyResponse> CreateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<LobbyResponse?> PreviewAsync(ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default);
    Task<LobbyResponse?> JoinAsync(ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default);
    Task<LobbyResponse?> LockAsync(ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default);
    Task<LobbyProvisionResponse?> VerifyAsync(ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default);
}
