using QuizArena.Identity.Domain.Entities;

namespace QuizArena.Identity.Api.Contracts;

/// <summary>
/// Public profile DTO for GET/PUT /api/users/me and PUT /api/users/me/picture (contracts/openapi/identity.yaml,
/// schema Profile). <see cref="Picture"/> is always serialised and is null when the player has no picture.
/// </summary>
public sealed record ProfileResponse(Guid Id, string Issuer, string Subject, string DisplayName, ProfilePictureResponse? Picture)
{
    public static ProfileResponse From(PlayerProfile profile, ProfilePictureInfo? picture)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new ProfileResponse(
            profile.Id,
            profile.Issuer,
            profile.Subject,
            profile.DisplayName,
            picture is null ? null : ProfilePictureResponse.From(picture));
    }
}
