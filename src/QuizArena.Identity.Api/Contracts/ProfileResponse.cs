using QuizArena.Identity.Domain.Entities;

namespace QuizArena.Identity.Api.Contracts;

/// <summary>Public profile DTO for GET/PUT /api/users/me (contracts/openapi/identity.yaml, schema Profile).</summary>
public sealed record ProfileResponse(Guid Id, string Issuer, string Subject, string DisplayName)
{
    public static ProfileResponse From(PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new ProfileResponse(profile.Id, profile.Issuer, profile.Subject, profile.DisplayName);
    }
}
