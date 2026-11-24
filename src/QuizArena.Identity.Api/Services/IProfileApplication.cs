using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Domain.Entities;

namespace QuizArena.Identity.Api.Services;

/// <summary>Identity application operations; the acting player is always the validated token's issuer + subject.</summary>
public interface IProfileApplication
{
    /// <summary>The caller's profile, or null when they have not saved one yet (GET /me then returns 404).</summary>
    Task<ProfileResponse?> GetMeAsync(AuthenticatedActor actor, CancellationToken cancellationToken);

    /// <summary>Creates or renames the caller's profile. Idempotent; safe when two first saves race.</summary>
    Task<ProfileResponse> UpsertMeAsync(AuthenticatedActor actor, UpdateProfileRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The caller's own picture (bytes included), or null when the caller has no profile or no picture.
    /// The owning profile is resolved only from the validated issuer + subject; no key, path or URL is accepted.
    /// </summary>
    Task<ProfilePicture?> GetMyPictureAsync(AuthenticatedActor actor, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the bytes against <paramref name="contentType"/> (<see cref="ProfilePictureFormat"/>), then atomically
    /// creates or replaces the caller's picture and returns the updated profile with its approved picture reference
    /// (content type, version, update time; no key, path or URL). The owning profile is resolved only from the validated
    /// issuer + subject. Throws <see cref="Domain.Exceptions.InvalidProfileException"/> (400) for empty, malformed or
    /// mismatched content before any lookup, <see cref="Domain.Exceptions.PlayerProfileNotFoundException"/> (404) when the
    /// caller has no profile and <see cref="Domain.Exceptions.ProfilePictureConflictException"/> (409) when a concurrent
    /// replace committed first.
    /// </summary>
    Task<ProfileResponse> ReplaceMyPictureAsync(AuthenticatedActor actor, string contentType, byte[] content, CancellationToken cancellationToken);
}
