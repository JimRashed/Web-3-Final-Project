namespace QuizArena.Notification.Domain.Exceptions;

/// <summary>
/// Base type for violations of the notification bounded context's business rules.
/// </summary>
public class NotificationDomainException : Exception
{
    public NotificationDomainException(string message) : base(message)
    {
    }
}
