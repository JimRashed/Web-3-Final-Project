namespace QuizArena.Gameplay.Domain.Exceptions;

/// <summary>
/// Raised when a match is saved for a lobby that already owns one (a concurrent start won the race).
/// The caller should load the lobby's existing match instead.
/// </summary>
public sealed class MatchAlreadyStartedException : MatchDomainException
{
    public MatchAlreadyStartedException(string lobbyId)
        : base($"Lobby '{lobbyId}' already has a match.")
    {
        LobbyId = lobbyId;
    }

    public string LobbyId { get; }
}
