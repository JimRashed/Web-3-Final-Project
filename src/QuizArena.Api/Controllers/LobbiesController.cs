using Microsoft.AspNetCore.Mvc;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Controllers;

[ApiController]
[Route("api/lobbies")]
public sealed class LobbiesController(ILobbyService lobbyService) : ControllerBase
{
    [HttpPost("{joinCode}/lock")]
    public async Task<ActionResult<LobbyProvisionResponse>> Lock(
        string joinCode,
        CancellationToken cancellationToken = default)
    {
        var response = await lobbyService.LockAsync(joinCode, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
