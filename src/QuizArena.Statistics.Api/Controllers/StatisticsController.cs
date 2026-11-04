using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Statistics.Api.Contracts;
using QuizArena.Statistics.Api.Security;
using QuizArena.Statistics.Api.Services;
using QuizArena.Statistics.Domain.Services;

namespace QuizArena.Statistics.Api.Controllers;

/// <summary>
/// Statistics routes. Results are written only by the Gameplay machine client; stats and history are self-only;
/// the leaderboard is visible to any signed-in player. Ownership and validation live in the application layer.
/// </summary>
[ApiController]
public sealed class StatisticsController(IStatisticsApplication statistics) : ControllerBase
{
    [HttpPost("api/results")]
    [Authorize(Policy = StatisticsPolicies.ResultsWrite)]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> RecordResult([FromBody] RecordResultRequest request, CancellationToken cancellationToken)
    {
        // New and identical-retry deliveries both answer 200 with an empty body.
        await statistics.RecordResultAsync(User.GetAuthenticatedActor(), request, cancellationToken);
        return Ok();
    }

    [HttpGet("api/players/{userId}/stats")]
    [Authorize(Policy = StatisticsPolicies.StatsRead)]
    [ProducesResponseType<StatsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<StatsResponse>> GetStats(string userId, CancellationToken cancellationToken) =>
        Ok(await statistics.GetStatsAsync(User.GetAuthenticatedActor(), userId, cancellationToken));

    [HttpGet("api/players/{userId}/matches")]
    [Authorize(Policy = StatisticsPolicies.StatsRead)]
    [ProducesResponseType<IReadOnlyList<HistoryItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<HistoryItemResponse>>> GetHistory(
        string userId,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = StatisticsCalculator.DefaultPageSize) =>
        Ok(await statistics.GetHistoryAsync(User.GetAuthenticatedActor(), userId, page, pageSize, cancellationToken));

    [HttpGet("api/leaderboard")]
    [Authorize(Policy = StatisticsPolicies.LeaderboardRead)]
    [ProducesResponseType<IReadOnlyList<LeaderResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<LeaderResponse>>> GetLeaderboard(
        CancellationToken cancellationToken,
        [FromQuery] int top = StatisticsCalculator.DefaultLeaderboardSize) =>
        Ok(await statistics.GetLeaderboardAsync(User.GetAuthenticatedActor(), top, cancellationToken));
}
