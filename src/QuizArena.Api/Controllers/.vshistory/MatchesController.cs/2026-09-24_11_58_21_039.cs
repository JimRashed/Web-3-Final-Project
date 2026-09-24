using Microsoft.AspNetCore.Mvc;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Controllers;

[ApiController]
[Route("matches")]
public class MatchesController : ControllerBase
{
    private readonly IGameplayService _service;

    public MatchesController(IGameplayService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMatchRequest req)
    {
        var result = await _service.CreateMatchAsync(req);
        return CreatedAtAction(nameof(Get), new { matchId = result.MatchId }, result);
    }

    [HttpGet("{matchId}")]
    public async Task<IActionResult> Get(string matchId)
    {
        var result = await _service.GetMatchAsync(matchId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{matchId}/next-question")]
    public async Task<IActionResult> NextQuestion(string matchId)
    {
        var result = await _service.GetNextQuestionAsync(matchId);
        return Ok(result);
    }

    [HttpPost("{matchId}/answers")]
    public async Task<IActionResult> SubmitAnswer(string matchId, [FromBody] SubmitAnswerRequest req)
    {
        var result = await _service.SubmitAnswerAsync(matchId, req);
        return Ok(result);
    }
}