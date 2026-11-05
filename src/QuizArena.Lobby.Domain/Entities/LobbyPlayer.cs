using QuizArena.Lobby.Domain.Exceptions;

namespace QuizArena.Lobby.Domain.Entities;

/// <summary>Immutable membership; the subject is preserved and compared exactly.</summary>
public sealed class LobbyPlayer
{
    public const int MaxSubjectLength = 200;

    private LobbyPlayer(Guid id, Guid lobbyId, string playerId)
    {
        Id = id;
        LobbyId = lobbyId;
        PlayerId = playerId;
    }

    public Guid Id { get; }
    public Guid LobbyId { get; }
    public string PlayerId { get; }

    public static LobbyPlayer Create(Guid lobbyId, string playerId, Guid? id = null)
    {
        var membershipId = id ?? Guid.NewGuid();
        EnsureId(membershipId, nameof(id));
        EnsureId(lobbyId, nameof(lobbyId));
        return new LobbyPlayer(membershipId, lobbyId, ValidateSubject(playerId, nameof(playerId)));
    }

    public static LobbyPlayer Rehydrate(Guid id, Guid lobbyId, string playerId) => Create(lobbyId, playerId, id);

    internal static string ValidateSubject(string subject, string field)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > MaxSubjectLength
            || !string.Equals(subject, subject.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidLobbyException(field, "Player subject must be nonblank, at most 200 characters and have no surrounding whitespace.");
        }

        return subject;
    }

    internal static void EnsureId(Guid id, string field)
    {
        if (id == Guid.Empty)
        {
            throw new InvalidLobbyException(field, "Lobby and membership identifiers must not be empty.");
        }
    }
}
