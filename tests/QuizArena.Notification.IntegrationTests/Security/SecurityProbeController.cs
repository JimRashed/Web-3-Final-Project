using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Notification.Api.Security;

namespace QuizArena.Notification.IntegrationTests.Security;

/// <summary>Test-only endpoints, one per named policy, added to the API only by <see cref="SecuredApiFactory"/>.</summary>
[ApiController]
[Route("__security-probe")]
public sealed class SecurityProbeController : ControllerBase
{
    [HttpGet("authenticated")]
    public IActionResult Authenticated() => Ok(Describe());

    [HttpGet("events.read")]
    [Authorize(Policy = NotificationPolicies.EventsRead)]
    public IActionResult EventsRead() => Ok(Describe());

    [HttpGet("preferences.read")]
    [Authorize(Policy = NotificationPolicies.PreferencesRead)]
    public IActionResult PreferencesRead() => Ok(Describe());

    [HttpGet("preferences.write")]
    [Authorize(Policy = NotificationPolicies.PreferencesWrite)]
    public IActionResult PreferencesWrite() => Ok(Describe());

    [HttpGet("events.write")]
    [Authorize(Policy = NotificationPolicies.EventsWrite)]
    public IActionResult EventsWrite() => Ok(Describe());

    private object Describe()
    {
        var actor = User.GetAuthenticatedActor();
        return new { subject = actor.Subject, issuer = actor.Issuer, kind = actor.Kind.ToString() };
    }
}
