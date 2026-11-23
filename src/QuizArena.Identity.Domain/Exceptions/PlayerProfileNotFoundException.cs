namespace QuizArena.Identity.Domain.Exceptions;

/// <summary>
/// The signed-in player has no stored profile (or it was removed mid-request). Maps to 404 at the API boundary.
/// </summary>
public sealed class PlayerProfileNotFoundException : IdentityDomainException
{
    public PlayerProfileNotFoundException()
        : base("The player profile was not found.")
    {
    }

    public PlayerProfileNotFoundException(Exception innerException)
        : base("The player profile was not found.", innerException)
    {
    }
}
