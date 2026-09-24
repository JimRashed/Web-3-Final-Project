namespace QuizArena.Domain.Notification.Entities;

public class EventLog
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}