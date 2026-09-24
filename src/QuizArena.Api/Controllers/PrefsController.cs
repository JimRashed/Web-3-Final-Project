using Microsoft.AspNetCore.Mvc;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Controllers;

[ApiController]
[Route("users")]
public class PrefsController : ControllerBase
{
    private readonly INotificationService _service;

    public PrefsController(INotificationService service) => _service = service;

    [HttpPut("{userId}/preferences")]
    public async Task<IActionResult> Update(string userId, [FromBody] UpdatePrefRequest req)
    {
        var result = await _service.UpdatePrefAsync(userId, req);
        return Ok(result);
    }
}