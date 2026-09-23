namespace QuizArena.Domain.Lobby.Entities;

public sealed class LobbyPlayer
{
    public Guid Id { get; set; }
    public Guid LobbyId { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public ActiveLobby Lobby { get; set; } = null!;
}
