using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Api.Services;

namespace QuizArena.Identity.Api.Controllers;

/// <summary>The signed-in player's own profile. The player is always the validated token's issuer + subject.</summary>
[ApiController]
[Route("api/users")]
public sealed class ProfilesController(IProfileApplication profiles) : ControllerBase
{
    [HttpGet("me")]
    [Authorize(Policy = IdentityPolicies.ProfilesRead)]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var profile = await profiles.GetMeAsync(User.GetAuthenticatedActor(), cancellationToken);
        return profile is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found", detail: "Save a display name to create your profile.")
            : Ok(profile);
    }

    [HttpPut("me")]
    [Authorize(Policy = IdentityPolicies.ProfilesWrite)]
    [Consumes("application/json")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ProfileResponse>> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await profiles.UpsertMeAsync(User.GetAuthenticatedActor(), request, cancellationToken));

    /// <summary>Retired: profiles are only readable by their owner through /api/users/me.</summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    public IActionResult RetiredProfileById(string id) => RetiredEndpoint.Gone(this);
}
