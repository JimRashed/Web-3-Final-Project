using QuizArena.Lobby.Domain.Enums;
using QuizArena.Lobby.Domain.Exceptions;
using QuizArena.Lobby.Domain.Services;

namespace QuizArena.Lobby.Domain.Entities;

public sealed class ActiveLobby
{
    public const int Capacity = 2;
    private readonly List<LobbyPlayer> _players;

    private ActiveLobby(Guid id, string joinCode, string hostId, LobbyStatus status, IEnumerable<LobbyPlayer> players)
    {
        Id = id;
        JoinCode = joinCode;
        HostId = hostId;
        Status = status;
        _players = new List<LobbyPlayer>(players);
        Players = _players.AsReadOnly();
    }

    public Guid Id { get; }
    public string JoinCode { get; }
    public string HostId { get; }
    public LobbyStatus Status { get; private set; }
    public IReadOnlyList<LobbyPlayer> Players { get; }

    public static ActiveLobby Create(string hostId, string joinCode, Guid? id = null)
    {
        var lobbyId = id ?? Guid.NewGuid();
        LobbyPlayer.EnsureId(lobbyId, nameof(id));
        var host = LobbyPlayer.ValidateSubject(hostId, nameof(hostId));
        var code = JoinCodeGenerator.Validate(joinCode);
        return new ActiveLobby(lobbyId, code, host, LobbyStatus.Open, [LobbyPlayer.Create(lobbyId, host)]);
    }

    /// <summary>Restores owned persisted data only after checking every aggregate invariant.</summary>
    public static ActiveLobby Rehydrate(Guid id, string joinCode, string hostId, LobbyStatus status, IEnumerable<LobbyPlayer> players)
    {
        LobbyPlayer.EnsureId(id, nameof(id));
        var host = LobbyPlayer.ValidateSubject(hostId, nameof(hostId));
        var code = JoinCodeGenerator.Validate(joinCode);
        if (!Enum.IsDefined(status))
        {
            throw new InvalidLobbyException(nameof(status), "Lobby state must be Open or Locked.");
        }

        var roster = players?.ToArray();
        if (roster is null || roster.Length is < 1 or > Capacity
            || roster.Any(player => player is null || player.LobbyId != id)
            || roster.Select(player => player.Id).Distinct().Count() != roster.Length
            || roster.Select(player => player.PlayerId).Distinct(StringComparer.Ordinal).Count() != roster.Length
            || roster.Count(player => string.Equals(player.PlayerId, host, StringComparison.Ordinal)) != 1
            || (status == LobbyStatus.Locked && roster.Length != Capacity))
        {
            throw new InvalidLobbyException(nameof(players), "Stored lobby membership violates lobby invariants.");
        }

        return new ActiveLobby(id, code, host, status, roster);
    }

    /// <summary>Returns false for an existing member in Open; every join to Locked conflicts.</summary>
    public bool Join(string playerId)
    {
        var subject = LobbyPlayer.ValidateSubject(playerId, nameof(playerId));
        if (Status != LobbyStatus.Open)
        {
            throw new LobbyConflictException("Players can join only an open lobby.");
        }

        if (_players.Any(player => string.Equals(player.PlayerId, subject, StringComparison.Ordinal)))
        {
            return false;
        }

        if (_players.Count == Capacity)
        {
            throw new LobbyConflictException("The lobby already has two players.");
        }

        _players.Add(LobbyPlayer.Create(Id, subject));
        return true;
    }

    /// <summary>Only the host may lock a two-player lobby; a repeated host lock is unchanged.</summary>
    public bool Lock(string playerId)
    {
        var subject = LobbyPlayer.ValidateSubject(playerId, nameof(playerId));
        if (!string.Equals(HostId, subject, StringComparison.Ordinal))
        {
            throw new LobbyForbiddenException("Only the lobby host can lock the lobby.");
        }

        if (Status == LobbyStatus.Locked)
        {
            return false;
        }

        if (_players.Count != Capacity)
        {
            throw new LobbyConflictException("Two players are required before locking the lobby.");
        }

        Status = LobbyStatus.Locked;
        return true;
    }
}
