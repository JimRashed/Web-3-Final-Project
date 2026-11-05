namespace QuizArena.Lobby.Api.Contracts;

public sealed class LobbyProvisionResponse
{
    public LobbyProvisionResponse(Guid lobbyId, string joinCode, string status, string hostUserId, IEnumerable<string> playerIds)
    {
        LobbyId = lobbyId;
        JoinCode = joinCode;
        Status = status;
        HostUserId = hostUserId;
        PlayerIds = Array.AsReadOnly(playerIds.ToArray());
    }

    public Guid LobbyId { get; }
    public string JoinCode { get; }
    public string Status { get; }
    public string HostUserId { get; }
    public IReadOnlyList<string> PlayerIds { get; }
}
