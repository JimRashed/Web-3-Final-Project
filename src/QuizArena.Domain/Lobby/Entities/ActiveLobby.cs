namespace QuizArena.Domain.Lobby.Entities;

public sealed class ActiveLobby
{
    public Guid Id { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public ICollection<LobbyPlayer> Players { get; set; } = new List<LobbyPlayer>();
}
