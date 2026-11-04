using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Notification.Api.Contracts;
using QuizArena.Notification.Api.Security;
using QuizArena.Notification.Api.Services;

namespace QuizArena.Notification.Api.Controllers;

/// <summary>
/// Notification channel preferences. The path subject must be the caller: a player can read and
/// change only their own preferences.
/// </summary>
[ApiController]
[Route("users/{userId}/preferences")]
public sealed class PreferencesController(IEventApplication events) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = NotificationPolicies.PreferencesRead)]
    [ProducesResponseType<UserPreferencesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<UserPreferencesResponse>> Get(string userId, CancellationToken cancellationToken)
    {
        var actor = User.GetAuthenticatedActor();
        if (!IsSelf(actor, userId))
        {
            return Forbid();
        }

        return Ok(await events.GetPreferencesAsync(actor.Issuer, actor.Subject, cancellationToken));
    }

    [HttpPut]
    [Authorize(Policy = NotificationPolicies.PreferencesWrite)]
    [ProducesResponseType<UserPreferencesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<UserPreferencesResponse>> Update(
        string userId,
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.GetAuthenticatedActor();
        if (!IsSelf(actor, userId))
        {
            return Forbid();
        }

        return Ok(await events.UpdatePreferencesAsync(actor.Issuer, actor.Subject, request, cancellationToken));
    }

    // Compared ordinally: a subject is an opaque, case-sensitive string.
    private static bool IsSelf(AuthenticatedActor actor, string userId) =>
        string.Equals(actor.Subject, userId, StringComparison.Ordinal);
}
