using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.QuestionBank.Api.Contracts;
using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.Api.Services;

namespace QuizArena.QuestionBank.Api.Controllers;

[ApiController]
[Route("internal/question-sets")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class InternalQuestionSetsController(IQuestionCatalogService service) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = QuestionBankPolicies.QuestionsPlay)]
    [ProducesResponseType<IReadOnlyList<QuestionSetResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<QuestionSetResponse>>> Create(
        [FromBody] QuestionSetRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.CreateSnapshotAsync(User.GetAuthenticatedActor(), request, cancellationToken));
    }
}
