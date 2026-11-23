using QuizArena.Identity.Domain.Entities;

namespace QuizArena.Identity.Api.Contracts;

/// <summary>
/// The approved picture reference inside a Profile (contracts/openapi/identity.yaml, schema ProfilePicture, SEC-03).
/// Never contains a storage key, path, URL or bytes.
/// </summary>
public sealed record ProfilePictureResponse(string ContentType, DateTime UpdatedAt, int Version)
{
    public static ProfilePictureResponse From(ProfilePictureInfo picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        return new ProfilePictureResponse(picture.ContentType, picture.UpdatedAt, picture.Version);
    }
}
