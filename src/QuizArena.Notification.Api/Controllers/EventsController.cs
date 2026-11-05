using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Notification.Api.Contracts;
using QuizArena.Notification.Api.Security;
using QuizArena.Notification.Api.Services;

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

    /// <summary>The signed-in player's events, newest first.</summary>
    [HttpGet]
    [Authorize(Policy = NotificationPolicies.EventsRead)]
    [ProducesResponseType<IReadOnlyList<EventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EventResponse>>> Mine(CancellationToken cancellationToken)
    {
        var actor = User.GetAuthenticatedActor();
        return Ok(await events.GetEventsAsync(actor.Issuer, actor.Subject, cancellationToken));
    }
}
