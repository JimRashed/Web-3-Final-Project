namespace QuizArena.Lobby.Data.Entities;

public sealed class LobbyPlayerRow
{
    public Guid Id { get; set; }
    public Guid LobbyId { get; set; }
    public string PlayerId { get; set; } = null!;
    public ActiveLobbyRow Lobby { get; set; } = null!;
}
