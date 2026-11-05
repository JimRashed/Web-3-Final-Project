using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Statistics.Api.Security;

namespace QuizArena.Statistics.IntegrationTests.Security;

/// <summary>Test-only endpoints, one per named policy, added to the API only by <see cref="SecuredApiFactory"/>.</summary>
[ApiController]
[Route("__security-probe")]
public sealed class SecurityProbeController : ControllerBase
{
    [HttpGet("authenticated")]
    public IActionResult Authenticated() => Ok(Describe());

    [HttpGet("stats.read")]
    [Authorize(Policy = StatisticsPolicies.StatsRead)]
    public IActionResult StatsRead() => Ok(Describe());

    [HttpGet("leaderboard.read")]
    [Authorize(Policy = StatisticsPolicies.LeaderboardRead)]
    public IActionResult LeaderboardRead() => Ok(Describe());

    [HttpGet("results.write")]
    [Authorize(Policy = StatisticsPolicies.ResultsWrite)]
    public IActionResult ResultsWrite() => Ok(Describe());

    private object Describe()
    {
        var actor = User.GetAuthenticatedActor();
        return new { subject = actor.Subject, issuer = actor.Issuer, kind = actor.Kind.ToString() };
    }
}
