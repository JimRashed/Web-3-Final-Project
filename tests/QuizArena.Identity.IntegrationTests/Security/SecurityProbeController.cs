using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Identity.Api.Security;

namespace QuizArena.Identity.IntegrationTests.Security;

/// <summary>Test-only endpoints, one per named policy, added to the API only by <see cref="SecuredApiFactory"/>.</summary>
[ApiController]
[Route("__security-probe")]
public sealed class SecurityProbeController : ControllerBase
{
    [HttpGet("authenticated")]
    public IActionResult Authenticated() => Ok(Describe());

    [HttpGet("profiles.read")]
    [Authorize(Policy = IdentityPolicies.ProfilesRead)]
    public IActionResult ProfilesRead() => Ok(Describe());

    [HttpGet("profiles.write")]
    [Authorize(Policy = IdentityPolicies.ProfilesWrite)]
    public IActionResult ProfilesWrite() => Ok(Describe());

    private object Describe()
    {
        var actor = User.GetAuthenticatedActor();
        return new { subject = actor.Subject, issuer = actor.Issuer, kind = actor.Kind.ToString() };
    }
}
