using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Gameplay.Api.Contracts;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Api.Services;

namespace QuizArena.Gameplay.Api.Controllers;

/// <summary>
/// Gameplay routes. Starting a match is host-only, every read and answer requires the caller to be a
/// participant, and the application service owns the status-code decisions.
/// </summary>
[ApiController]
[Route("matches")]
public sealed class MatchesController(IGameplayApplication matches) : ControllerBase
{
    /// <summary>Starts the lobby's match; a repeated start returns the existing match with 200.</summary>
    [HttpPost]
    [Authorize(Policy = GameplayPolicies.MatchesWrite)]
    [ProducesResponseType<MatchResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<MatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MatchResponse>> Create(CreateMatchRequest request, CancellationToken cancellationToken)
    {
        var actor = User.GetAuthenticatedActor();
        var (match, created) = await matches.CreateAsync(actor, request, cancellationToken);

        return created
            ? Created($"/matches/{match.MatchId}", match)
            : Ok(match);
    }

    [HttpGet("{matchId}")]
    [Authorize(Policy = GameplayPolicies.MatchesRead)]
    [ProducesResponseType<MatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MatchResponse>> Get(string matchId, CancellationToken cancellationToken)
    {
        var actor = User.GetAuthenticatedActor();
        return Ok(await matches.GetAsync(actor, matchId, cancellationToken));
    }

    /// <summary>The current question without the correct answer; reading never advances the match.</summary>
    [HttpGet("{matchId}/next-question")]
    [Authorize(Policy = GameplayPolicies.MatchesRead)]
    [ProducesResponseType<CurrentQuestionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CurrentQuestionResponse>> NextQuestion(string matchId, CancellationToken cancellationToken)
    {
        var actor = User.GetAuthenticatedActor();
        return Ok(await matches.GetCurrentQuestionAsync(actor, matchId, cancellationToken));
    }

    [HttpPost("{matchId}/answers")]
    [Authorize(Policy = GameplayPolicies.MatchesWrite)]
    [ProducesResponseType<AnswerOutcomeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AnswerOutcomeResponse>> Answer(
        string matchId,
        SubmitAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.GetAuthenticatedActor();
        return Ok(await matches.SubmitAnswerAsync(actor, matchId, request, cancellationToken));
    }
}
