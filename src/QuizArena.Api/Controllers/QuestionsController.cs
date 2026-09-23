using Microsoft.AspNetCore.Mvc;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Controllers;

[ApiController]
[Route("api/questions")]
public sealed class QuestionsController(IQuestionService questionService) : ControllerBase
{
    [HttpGet("generate")]
    public async Task<ActionResult<IReadOnlyList<QuestionDto>>> Generate(
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return BadRequest("Count must be greater than zero.");
        }

        return Ok(await questionService.GenerateAsync(count, cancellationToken));
    }
}
