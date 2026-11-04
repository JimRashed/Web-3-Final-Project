namespace QuizArena.Notification.Domain.Exceptions;

/// <summary>
/// Raised when an event cannot be created because its data violates notification rules.
/// </summary>
public sealed class InvalidEventException : NotificationDomainException
{
    public InvalidEventException(string message) : base(message)
    {
    }
}
