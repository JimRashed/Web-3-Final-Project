using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Api.Services;
using QuizArena.Identity.Domain.Entities;

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
    [RequireJsonContentType]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<ActionResult<ProfileResponse>> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await profiles.UpsertMeAsync(User.GetAuthenticatedActor(), request, cancellationToken));

    /// <summary>The caller's own picture bytes with the stored validated Content-Type (SEC-01, SEC-08).</summary>
    [HttpGet("me/picture")]
    [Authorize(Policy = IdentityPolicies.ProfilesRead)]
    [PrivatePictureResponse]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "image/png", "image/jpeg", "image/webp")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetMyPicture(CancellationToken cancellationToken)
    {
        var picture = await profiles.GetMyPictureAsync(User.GetAuthenticatedActor(), cancellationToken);
        return picture is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Picture not found", detail: "No profile or no picture for the caller.")
            : File(picture.Content, picture.ContentType);
    }

    /// <summary>
    /// Replaces the caller's picture from a raw PNG, JPEG or WebP body. Order: 401/403, 415, 413, 400, 404, 409, 200;
    /// 400/404/409/500/503 come from <see cref="IdentityExceptionHandler"/>.
    /// </summary>
    [HttpPut("me/picture")]
    [Authorize(Policy = IdentityPolicies.ProfilesWrite)]
    [RequireImageContentType]
    [PrivatePictureResponse]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<IActionResult> ReplaceMyPicture(CancellationToken cancellationToken)
    {
        if (!RequireImageContentTypeAttribute.TryGetImageMediaType(Request.ContentType, out var contentType))
        {
            // The resource filter already answered 415; kept so the action never trusts an unchecked type.
            return RequireImageContentTypeAttribute.UnsupportedMediaType(HttpContext);
        }

        var content = await PictureRequestBody.ReadAsync(Request, ProfilePicture.MaxContentLength, cancellationToken);
        if (content is null)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Payload too large",
                detail: "Image exceeds 2,097,152 bytes.",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.14");
        }

        return Ok(await profiles.ReplaceMyPictureAsync(User.GetAuthenticatedActor(), contentType, content, cancellationToken));
    }

    /// <summary>Retired: profiles are only readable by their owner through /api/users/me.</summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    public IActionResult RetiredProfileById(string id) => RetiredEndpoint.Gone(this);
}
