using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Lobby.Api.Security;

namespace QuizArena.Lobby.IntegrationTests.Security;

/// <summary>Test-only endpoints, one per named policy, added to the API only by <see cref="SecuredApiFactory"/>.</summary>
[ApiController]
[Route("__security-probe")]
public sealed class SecurityProbeController : ControllerBase
{
    [HttpGet("authenticated")]
    public IActionResult Authenticated() => Ok(Describe());

    [HttpGet("lobbies.read")]
    [Authorize(Policy = LobbyPolicies.LobbiesRead)]
    public IActionResult LobbiesRead() => Ok(Describe());

    [HttpGet("lobbies.write")]
    [Authorize(Policy = LobbyPolicies.LobbiesWrite)]
    public IActionResult LobbiesWrite() => Ok(Describe());

    [HttpGet("lobbies.verify")]
    [Authorize(Policy = LobbyPolicies.LobbiesVerify)]
    public IActionResult LobbiesVerify() => Ok(Describe());

    private object Describe()
    {
        var actor = User.GetAuthenticatedActor();
        return new { subject = actor.Subject, issuer = actor.Issuer, kind = actor.Kind.ToString() };
    }
}
