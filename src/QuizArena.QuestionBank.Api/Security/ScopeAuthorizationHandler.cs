using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace QuizArena.QuestionBank.Api.Security;

public sealed class ScopeRequirement(string scope, ActorKind actor) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;

    public ActorKind Actor { get; } = actor;
}

public sealed class ScopeAuthorizationHandler(IOptions<QuizArenaAuthenticationOptions> options)
    : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var settings = options.Value;
        var (expectedClient, expectedActor) = requirement.Actor == ActorKind.User
            ? (settings.BrowserClientId, QuizArenaClaims.UserActor)
            : (settings.MachineClientId, QuizArenaClaims.MachineActor);
        var user = context.User;

        if (!string.IsNullOrEmpty(expectedClient)
            && string.Equals(user.FindFirst(QuizArenaClaims.AuthorizedParty)?.Value, expectedClient, StringComparison.Ordinal)
            && string.Equals(user.FindFirst(QuizArenaClaims.Actor)?.Value, expectedActor, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(user.FindFirst(QuizArenaClaims.Subject)?.Value)
            && user.HasScope(requirement.Scope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
