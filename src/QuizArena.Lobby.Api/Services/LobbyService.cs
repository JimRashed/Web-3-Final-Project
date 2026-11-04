using System.Security.Claims;
using QuizArena.Lobby.Api.Contracts;
using QuizArena.Lobby.Api.Security;
using QuizArena.Lobby.Data.Repositories;
using QuizArena.Lobby.Domain.Entities;
using QuizArena.Lobby.Domain.Exceptions;
using QuizArena.Lobby.Domain.Services;

namespace QuizArena.Lobby.Api.Services;

public sealed class LobbyService(ILobbyRepository repository) : ILobbyService
{
    public async Task<LobbyResponse> CreateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var actor = RequireActor(principal, ActorKind.User, cancellationToken);
        var lobby = await repository.CreateAsync(actor.Subject, cancellationToken);
        return ToPublic(lobby, actor.Subject);
    }

    public async Task<LobbyResponse?> PreviewAsync(ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default)
    {
        var actor = RequireActor(principal, ActorKind.User, cancellationToken);
        JoinCodeGenerator.Validate(joinCode);
        var lobby = await repository.GetByJoinCodeAsync(joinCode, cancellationToken);
        return lobby is null ? null : ToPublic(lobby, actor.Subject);
    }

    public async Task<LobbyResponse?> JoinAsync(ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default)
    {
        var actor = RequireActor(principal, ActorKind.User, cancellationToken);
        JoinCodeGenerator.Validate(joinCode);
        var lobby = await repository.JoinAsync(joinCode, actor.Subject, cancellationToken);
        return lobby is null ? null : ToPublic(lobby, actor.Subject);
    }

    public async Task<LobbyResponse?> LockAsync(ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default)
    {
        var actor = RequireActor(principal, ActorKind.User, cancellationToken);
        JoinCodeGenerator.Validate(joinCode);
        // This callback captures only an immutable trusted subject and is safe for persistence retries.
        var lobby = await repository.SaveAsync(joinCode, lobby => lobby.Lock(actor.Subject), cancellationToken);
        return lobby is null ? null : ToPublic(lobby, actor.Subject);
    }

    public async Task<LobbyProvisionResponse?> VerifyAsync(ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default)
    {
        RequireActor(principal, ActorKind.Machine, cancellationToken);
        JoinCodeGenerator.Validate(joinCode);
        var lobby = await repository.GetByJoinCodeAsync(joinCode, cancellationToken);
        return lobby is null ? null : new LobbyProvisionResponse(lobby.Id, lobby.JoinCode, lobby.Status.ToString(), lobby.HostId, lobby.Players.Select(player => player.PlayerId));
    }

    private static AuthenticatedActor RequireActor(ClaimsPrincipal principal, ActorKind requiredKind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        const string message = "An authenticated QuizArena actor is required.";
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException(message);
        }

        AuthenticatedActor actor;
        try
        {
            actor = principal.GetAuthenticatedActor();
        }
        catch (InvalidOperationException exception)
        {
            throw new UnauthorizedAccessException(message, exception);
        }

        if (actor.Kind != requiredKind)
        {
            throw new LobbyForbiddenException("This actor cannot perform this lobby operation.");
        }

        return actor;
    }

    private static LobbyResponse ToPublic(ActiveLobby lobby, string subject) => new(
        lobby.Id, lobby.JoinCode, lobby.Status.ToString(), lobby.Players.Count,
        lobby.Players.Any(player => string.Equals(player.PlayerId, subject, StringComparison.Ordinal)),
        string.Equals(lobby.HostId, subject, StringComparison.Ordinal));
}
