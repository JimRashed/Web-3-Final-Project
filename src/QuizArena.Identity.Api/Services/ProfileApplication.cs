using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Repositories;

namespace QuizArena.Identity.Api.Services;

public sealed class ProfileApplication(IPlayerProfileRepository profiles, TimeProvider clock) : IProfileApplication
{
    public async Task<ProfileResponse?> GetMeAsync(AuthenticatedActor actor, CancellationToken cancellationToken)
    {
        EnsurePlayer(actor);
        var profile = await profiles.FindByIdentityAsync(actor.Issuer, actor.Subject, cancellationToken);
        return profile is null ? null : ProfileResponse.From(profile);
    }

    public async Task<ProfileResponse> UpsertMeAsync(AuthenticatedActor actor, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        EnsurePlayer(actor);
        ArgumentNullException.ThrowIfNull(request);

        var displayName = request.DisplayName ?? string.Empty;
        var now = clock.GetUtcNow().UtcDateTime;

        var renamed = await profiles.RenameAsync(actor.Issuer, actor.Subject, displayName, now, cancellationToken);
        if (renamed is not null)
        {
            return ProfileResponse.From(renamed);
        }

        // Domain validation (display name rules) runs here, before anything is written.
        var candidate = PlayerProfile.Create(actor.Issuer, actor.Subject, displayName, now);
        var stored = await profiles.AddIfAbsentAsync(candidate, cancellationToken);
        if (stored.Id != candidate.Id)
        {
            // Another first save won the race; apply this request's name to the stored profile.
            stored = await profiles.RenameAsync(actor.Issuer, actor.Subject, displayName, now, cancellationToken) ?? stored;
        }

        return ProfileResponse.From(stored);
    }

    private static void EnsurePlayer(AuthenticatedActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.User)
        {
            throw new InvalidOperationException("Profile operations are only available to signed-in players.");
        }
    }
}
