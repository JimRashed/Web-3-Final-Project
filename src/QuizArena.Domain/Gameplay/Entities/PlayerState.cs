namespace QuizArena.Domain.Gameplay.Entities;

public class PlayerState
{
    public int Id { get; set; }
    public string MatchId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int Score { get; set; }
}