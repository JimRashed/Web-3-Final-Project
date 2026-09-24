using Microsoft.AspNetCore.Mvc;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Controllers;

[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly INotificationService _service;

    public EventsController(INotificationService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest req)
    {
        var result = await _service.LogEventAsync(req);
        return Accepted(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetByUser([FromQuery] string userId)
    {
        var result = await _service.GetUserEventsAsync(userId);
        return Ok(result);
    }
}