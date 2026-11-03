using System.Security.Claims;

namespace QuizArena.Statistics.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static AuthenticatedActor GetAuthenticatedActor(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var subjectClaim = principal.FindFirst(QuizArenaClaims.Subject);
        var issuer = principal.FindFirst(QuizArenaClaims.Issuer)?.Value ?? subjectClaim?.Issuer;
        ActorKind? kind = principal.FindFirst(QuizArenaClaims.Actor)?.Value switch
        {
            QuizArenaClaims.UserActor => ActorKind.User,
            QuizArenaClaims.MachineActor => ActorKind.Machine,
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subjectClaim?.Value) || kind is null)
        {
            throw new InvalidOperationException("The principal is not an authenticated QuizArena actor.");
        }

        return new AuthenticatedActor(issuer, subjectClaim.Value, kind.Value);
    }

    public static bool HasScope(this ClaimsPrincipal principal, string scope)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.Claims
            .Where(claim => claim.Type is QuizArenaClaims.Scope or QuizArenaClaims.ScopeList)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Any(value => string.Equals(value, scope, StringComparison.Ordinal));
    }
}
