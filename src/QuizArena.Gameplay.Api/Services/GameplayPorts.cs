using QuizArena.Gameplay.Domain.Entities;

namespace QuizArena.Gameplay.Api.Services;

/// <summary>What Gameplay needs to know about a lobby before it starts a match.</summary>
public sealed record LobbyState(string LobbyId, string HostSubject, IReadOnlyList<string> Members, bool IsLocked);

/// <summary>Lobby service port; the HTTP adapter supplies the machine token.</summary>
public interface ILobbyGateway
{
    /// <summary>Returns the lobby, or null when the join code is unknown.</summary>
    Task<LobbyState?> GetAsync(string joinCode, CancellationToken cancellationToken);
}

/// <summary>QuestionBank service port; the HTTP adapter supplies the machine token.</summary>
public interface IQuestionCatalog
{
    /// <summary>Exactly <paramref name="count"/> immutable question snapshots.</summary>
    Task<IReadOnlyList<QuestionSnapshot>> SnapshotAsync(int count, CancellationToken cancellationToken);
}

/// <summary>
/// A request the API layer must translate into a specific status code (unknown lobby, lobby not
/// locked, caller not the host, and so on) without leaking transport concerns into the domain.
/// </summary>
public sealed class GameplayRequestException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
