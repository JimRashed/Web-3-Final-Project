namespace QuizArena.Domain.Notification.Entities;

public class UserPref
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
}