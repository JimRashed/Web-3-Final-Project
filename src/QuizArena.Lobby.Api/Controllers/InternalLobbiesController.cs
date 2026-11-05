using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Lobby.Api.Contracts;
using QuizArena.Lobby.Api.Errors;
using QuizArena.Lobby.Api.Security;
using QuizArena.Lobby.Api.Services;

namespace QuizArena.Lobby.Api.Controllers;

[ApiController]
[Route("internal/lobbies")]
public sealed class InternalLobbiesController(ILobbyService service) : ControllerBase
{
    [HttpGet("{joinCode}")]
    [Authorize(Policy = LobbyPolicies.LobbiesVerify)]
    public async Task<ActionResult<LobbyProvisionResponse>> Verify(string joinCode, CancellationToken cancellationToken)
    {
        var lobby = await service.VerifyAsync(User, joinCode, cancellationToken);
        return lobby is null ? LobbyProblems.Result(HttpContext, 404) : Ok(lobby);
    }
}
