using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Notification.Api.Contracts;
using QuizArena.Notification.Api.Security;
using QuizArena.Notification.Api.Services;
using QuizArena.Notification.Domain.Exceptions;

namespace QuizArena.Notification.Api.Controllers;

/// <summary>
/// Notification events. <c>POST</c> is the machine-only ingestion route; <c>GET</c> returns the
/// signed-in player's own events.
/// </summary>
[ApiController]
[Route("events")]
public sealed class EventsController(IEventApplication events) : ControllerBase
{
    /// <summary>Persists an event produced by Gameplay. Identical replays return the stored event.</summary>
    [HttpPost]
    [Authorize(Policy = NotificationPolicies.EventsWrite)]
    [ProducesResponseType<EventResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EventResponse>> Create(CreateEventRequest request, CancellationToken cancellationToken)
    {
        var actor = User.GetAuthenticatedActor();
        var stored = await events.PublishAsync(request, actor.Issuer, cancellationToken);
        return Accepted(stored);
    }

    /// <summary>
    /// One page of the signed-in player's events, newest first. The optional legacy <c>userId</c> must be the
    /// caller (ordinal); paging is validated first, so invalid paging is 400 even for another player.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = NotificationPolicies.EventsRead)]
    [ProducesResponseType<IReadOnlyList<EventResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<EventResponse>>> Mine(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? userId = null)
    {
        InvalidPagingException.ThrowIfInvalid(page, pageSize);
        var actor = User.GetAuthenticatedActor();
        if (userId is not null && !string.Equals(userId, actor.Subject, StringComparison.Ordinal))
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "Players can only read their own events.");
        }

        return Ok(await events.GetEventsAsync(actor.Issuer, actor.Subject, page, pageSize, cancellationToken));
    }
}
