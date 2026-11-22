namespace QuizArena.Identity.Domain.Exceptions;

/// <summary>
/// A concurrent picture replacement committed first (SEC-07: first committer wins). The caller may retry.
/// Maps to 409 at the API boundary.
/// </summary>
public sealed class ProfilePictureConflictException : IdentityDomainException
{
    public ProfilePictureConflictException()
        : base("The profile picture was changed by another request. Retry the upload.")
    {
    }

    public ProfilePictureConflictException(Exception innerException)
        : base("The profile picture was changed by another request. Retry the upload.", innerException)
    {
    }
}
