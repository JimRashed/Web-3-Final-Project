namespace QuizArena.Notification.Domain.Exceptions;

/// <summary>Raised when a page or page size is outside the contract's bounds (HTTP 400).</summary>
public sealed class InvalidPagingException : NotificationDomainException
{
    public const int MaxPageSize = 100;

    public InvalidPagingException(string field, string message) : base(message)
    {
        Field = field;
    }

    public string Field { get; }

    /// <summary>Throws unless page &gt;= 1 and 1 &lt;= pageSize &lt;= 100.</summary>
    public static void ThrowIfInvalid(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new InvalidPagingException("page", "Page must be 1 or greater.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            throw new InvalidPagingException("pageSize", $"Page size must be between 1 and {MaxPageSize}.");
        }
    }
}
