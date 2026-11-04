using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuizArena.Identity.Api.Controllers;

/// <summary>
/// Former password endpoints. Keycloak now owns registration and sign-in. These actions take no body parameter,
/// so credentials are never bound, validated, hashed, logged or stored, and they always answer 410.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class RetiredAuthController : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    public IActionResult RetiredRegister() => RetiredEndpoint.Gone(this);

    [HttpPost("login")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    public IActionResult RetiredLogin() => RetiredEndpoint.Gone(this);
}

internal static class RetiredEndpoint
{
    public const string Detail = "Use configured OIDC provider sign-in and /api/users/me.";

    public static IActionResult Gone(ControllerBase controller) =>
        controller.Problem(statusCode: StatusCodes.Status410Gone, title: "Endpoint retired", detail: Detail);
}
