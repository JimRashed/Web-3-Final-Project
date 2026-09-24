namespace QuizArena.Domain.Gameplay.Entities;

public class ActiveMatch
{
    public string MatchId { get; set; } = string.Empty;
    public string PlayerAId { get; set; } = string.Empty;
    public string PlayerBId { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public int PlayerAScore { get; set; }
    public int PlayerBScore { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}