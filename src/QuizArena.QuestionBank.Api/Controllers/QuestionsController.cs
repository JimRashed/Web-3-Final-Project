using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.QuestionBank.Api.Contracts;
using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.Api.Services;

namespace QuizArena.QuestionBank.Api.Controllers;

[ApiController]
[Route("api/questions")]
public sealed class QuestionsController(IQuestionCatalogService service) : ControllerBase
{
    [HttpGet("generate")]
    [Authorize(Policy = QuestionBankPolicies.QuestionsRead)]
    [ProducesResponseType<IReadOnlyList<QuestionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<QuestionResponse>>> Generate(
        [FromQuery, Range(1, 50)] int count = 10, CancellationToken cancellationToken = default)
    {
        return Ok(await service.GenerateAsync(User.GetAuthenticatedActor(), count, cancellationToken));
    }
}
