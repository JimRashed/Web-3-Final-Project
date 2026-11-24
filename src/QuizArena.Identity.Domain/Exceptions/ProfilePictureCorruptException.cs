namespace QuizArena.Identity.Domain.Exceptions;

/// <summary>
/// A stored picture row breaks a picture invariant (unsupported content type, version below 1, empty or oversized
/// content, non-UTC timestamp). Raised by the store's read path so corrupt data fails loudly instead of being returned.
/// This is a server-side data fault, not a client error; the inner <see cref="InvalidProfileException"/> names the field.
/// </summary>
public sealed class ProfilePictureCorruptException : IdentityDomainException
{
    public ProfilePictureCorruptException(InvalidProfileException innerException)
        : base("The stored profile picture is invalid.", innerException)
    {
    }
}
