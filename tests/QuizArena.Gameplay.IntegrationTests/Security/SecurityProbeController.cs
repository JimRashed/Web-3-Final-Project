using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Gameplay.Api.Security;

namespace QuizArena.Gameplay.IntegrationTests.Security;

/// <summary>Test-only endpoints, one per named policy, added to the API only by <see cref="SecuredApiFactory"/>.</summary>
[ApiController]
[Route("__security-probe")]
public sealed class SecurityProbeController : ControllerBase
{
    [HttpGet("authenticated")]
    public IActionResult Authenticated() => Ok(Describe());

    [HttpGet("matches.read")]
    [Authorize(Policy = GameplayPolicies.MatchesRead)]
    public IActionResult MatchesRead() => Ok(Describe());

    [HttpGet("matches.write")]
    [Authorize(Policy = GameplayPolicies.MatchesWrite)]
    public IActionResult MatchesWrite() => Ok(Describe());

    private object Describe()
    {
        var actor = User.GetAuthenticatedActor();
        return new { subject = actor.Subject, issuer = actor.Issuer, kind = actor.Kind.ToString() };
    }
}
