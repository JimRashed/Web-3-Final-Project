using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;

namespace QuizArena.Identity.Api.Services;

/// <summary>Identity application operations; the acting player is always the validated token's issuer + subject.</summary>
public interface IProfileApplication
{
    /// <summary>The caller's profile, or null when they have not saved one yet (GET /me then returns 404).</summary>
    Task<ProfileResponse?> GetMeAsync(AuthenticatedActor actor, CancellationToken cancellationToken);

    /// <summary>Creates or renames the caller's profile. Idempotent; safe when two first saves race.</summary>
    Task<ProfileResponse> UpsertMeAsync(AuthenticatedActor actor, UpdateProfileRequest request, CancellationToken cancellationToken);
}
