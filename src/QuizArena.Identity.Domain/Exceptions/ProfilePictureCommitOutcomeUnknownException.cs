namespace QuizArena.Identity.Domain.Exceptions;

/// <summary>
/// A picture replacement's commit step failed and the store could not verify afterwards whether the transaction
/// committed (the verification read itself failed or timed out). The new picture may or may not be stored, so a retry
/// may double-count: it can add one more version on top of a write that did commit. The inner exception is the
/// original commit failure. The Api maps it to a safe 500 asking the client to reload its profile before retrying (#144).
/// </summary>
public sealed class ProfilePictureCommitOutcomeUnknownException : IdentityDomainException
{
    public ProfilePictureCommitOutcomeUnknownException(Exception innerException)
        : base("The profile picture upload outcome could not be confirmed. A retry may add one more version.", innerException)
    {
    }
}
