using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Exceptions;
using QuizArena.Identity.Domain.Repositories;

namespace QuizArena.Identity.Api.Services;

public sealed class ProfileApplication(IPlayerProfileRepository profiles, IProfilePictureStore pictures, TimeProvider clock) : IProfileApplication
{
    public async Task<ProfilePicture?> GetMyPictureAsync(AuthenticatedActor actor, CancellationToken cancellationToken)
    {
        EnsurePlayer(actor);

        // The profile id always comes from the validated issuer + subject (exact, case-sensitive lookup).
        var profile = await profiles.FindByIdentityAsync(actor.Issuer, actor.Subject, cancellationToken);
        return profile is null ? null : await pictures.GetAsync(profile.Id, cancellationToken);
    }

    public async Task<ProfileResponse> ReplaceMyPictureAsync(
        AuthenticatedActor actor,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        EnsurePlayer(actor);

        // Bytes must really be the declared image type; a rejected upload never reaches the database (400 before 404).
        ProfilePictureFormat.Validate(contentType, content);

        // No implicit profile creation: a picture always belongs to an existing profile of exactly this identity.
        var profile = await profiles.FindByIdentityAsync(actor.Issuer, actor.Subject, cancellationToken)
            ?? throw new PlayerProfileNotFoundException();
        var now = clock.GetUtcNow().UtcDateTime;
        var picture = await pictures.ReplaceAsync(profile.Id, contentType, content, now, cancellationToken);
        return ProfileResponse.From(profile, picture);
    }

    public async Task<ProfileResponse?> GetMeAsync(AuthenticatedActor actor, CancellationToken cancellationToken)
    {
        EnsurePlayer(actor);
        var profile = await profiles.FindByIdentityAsync(actor.Issuer, actor.Subject, cancellationToken);
        return profile is null ? null : await WithPictureAsync(profile, cancellationToken);
    }

    public async Task<ProfileResponse> UpsertMeAsync(AuthenticatedActor actor, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        EnsurePlayer(actor);
        ArgumentNullException.ThrowIfNull(request);

        var displayName = request.DisplayName ?? string.Empty;
        var now = clock.GetUtcNow().UtcDateTime;

        // Picture metadata is read BEFORE any write (#144 review): RenameAsync/AddIfAbsentAsync commit on return, so a
        // failed metadata read afterwards would report 500/503 for a name change that was already saved.
        var existing = await profiles.FindByIdentityAsync(actor.Issuer, actor.Subject, cancellationToken);
        if (existing is not null)
        {
            var picture = await pictures.GetInfoAsync(existing.Id, cancellationToken);
            var renamed = await profiles.RenameAsync(actor.Issuer, actor.Subject, displayName, now, cancellationToken);
            if (renamed is not null)
            {
                return ProfileResponse.From(renamed, picture);
            }

            // The profile disappeared between the lookup and the rename: create it again below.
        }

        // Domain validation (display name rules) runs here, before anything is written.
        var candidate = PlayerProfile.Create(actor.Issuer, actor.Subject, displayName, now);
        var stored = await profiles.AddIfAbsentAsync(candidate, cancellationToken);
        if (stored.Id == candidate.Id)
        {
            // A brand-new profile cannot have a picture yet.
            return ProfileResponse.From(stored, null);
        }

        // Another first save won the race; read its picture metadata, then apply this request's name to it.
        var winnerPicture = await pictures.GetInfoAsync(stored.Id, cancellationToken);
        stored = await profiles.RenameAsync(actor.Issuer, actor.Subject, displayName, now, cancellationToken) ?? stored;
        return ProfileResponse.From(stored, winnerPicture);
    }

    private async Task<ProfileResponse> WithPictureAsync(PlayerProfile profile, CancellationToken cancellationToken) =>
        ProfileResponse.From(profile, await pictures.GetInfoAsync(profile.Id, cancellationToken));

    private static void EnsurePlayer(AuthenticatedActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.User)
        {
            throw new InvalidOperationException("Profile operations are only available to signed-in players.");
        }
    }
}
