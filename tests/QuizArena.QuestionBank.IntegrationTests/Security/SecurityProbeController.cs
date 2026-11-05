using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.QuestionBank.Api.Security;

namespace QuizArena.QuestionBank.IntegrationTests.Security;

/// <summary>Test-only endpoints, one per named policy, added to the API only by <see cref="SecuredApiFactory"/>.</summary>
[ApiController]
[Route("__security-probe")]
public sealed class SecurityProbeController : ControllerBase
{
    [HttpGet("authenticated")]
    public IActionResult Authenticated() => Ok(Describe());

    [HttpGet("questions.read")]
    [Authorize(Policy = QuestionBankPolicies.QuestionsRead)]
    public IActionResult QuestionsRead() => Ok(Describe());

    [HttpGet("questions.play")]
    [Authorize(Policy = QuestionBankPolicies.QuestionsPlay)]
    public IActionResult QuestionsPlay() => Ok(Describe());

    private object Describe()
    {
        var actor = User.GetAuthenticatedActor();
        return new { subject = actor.Subject, issuer = actor.Issuer, kind = actor.Kind.ToString() };
    }
}
