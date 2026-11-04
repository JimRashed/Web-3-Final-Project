using QuizArena.Lobby.Domain.Enums;

namespace QuizArena.Lobby.Data.Entities;

public sealed class ActiveLobbyRow
{
    public Guid Id { get; set; }
    public string JoinCode { get; set; } = null!;
    public string HostId { get; set; } = null!;
    public LobbyStatus Status { get; set; }
    public long WriteRevision { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<LobbyPlayerRow> Players { get; set; } = [];
}
