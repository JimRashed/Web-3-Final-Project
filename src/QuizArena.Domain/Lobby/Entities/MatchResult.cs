namespace QuizArena.Domain.Statistics.Entities;

public class MatchResult
{
    public int Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public int Score { get; set; }
    public bool Won { get; set; }
    public DateTime PlayedAt { get; set; }
}
