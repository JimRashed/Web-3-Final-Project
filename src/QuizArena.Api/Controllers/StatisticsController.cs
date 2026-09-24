using Microsoft.AspNetCore.Mvc;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;

namespace QuizArena.Statistics.Api.Controllers;

[ApiController]
[Route("api")]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _stats;

    public StatisticsController(IStatisticsService stats)
    {
        _stats = stats;
    }

    [HttpPost("results")]
    public async Task<IActionResult> RecordResult(RecordResultRequest request)
    {
        await _stats.RecordResultAsync(request);
        return Ok();
    }

    [HttpGet("players/{userId}/stats")]
    public async Task<IActionResult> GetStats(Guid userId) =>
        Ok(await _stats.GetStatsAsync(userId));

    [HttpGet("players/{userId}/matches")]
    public async Task<IActionResult> GetHistory(Guid userId) =>
        Ok(await _stats.GetHistoryAsync(userId));

    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(int top = 10) =>
        Ok(await _stats.GetLeaderboardAsync(top));
}