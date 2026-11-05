using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using QuizArena.Lobby.Api.Contracts;
using QuizArena.Lobby.Api.Errors;
using QuizArena.Lobby.Api.Security;
using QuizArena.Lobby.Api.Services;

namespace QuizArena.Lobby.Api.Controllers;

[ApiController]
[Route("api/lobbies")]
public sealed class LobbiesController(ILobbyService service) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = LobbyPolicies.LobbiesWrite)]
    public async Task<ActionResult<LobbyResponse>> Create([FromBody] EmptyLobbyRequest request, CancellationToken cancellationToken)
    {
        var lobby = await service.CreateAsync(User, cancellationToken);
        return Created($"/api/lobbies/{lobby.JoinCode}", lobby);
    }

    [HttpGet("{joinCode}")]
    [Authorize(Policy = LobbyPolicies.LobbiesRead)]
    public async Task<ActionResult<LobbyResponse>> Preview(string joinCode, CancellationToken cancellationToken)
    {
        var lobby = await service.PreviewAsync(User, joinCode, cancellationToken);
        return lobby is null ? LobbyProblems.Result(HttpContext, 404) : Ok(lobby);
    }

    [HttpPost("{joinCode}/join")]
    [Authorize(Policy = LobbyPolicies.LobbiesWrite)]
    public async Task<ActionResult<LobbyResponse>> Join(string joinCode, [FromBody] EmptyLobbyRequest request, CancellationToken cancellationToken)
    {
        var lobby = await service.JoinAsync(User, joinCode, cancellationToken);
        return lobby is null ? LobbyProblems.Result(HttpContext, 404) : Ok(lobby);
    }

    [HttpPost("{joinCode}/lock")]
    [Authorize(Policy = LobbyPolicies.LobbiesWrite)]
    public async Task<ActionResult<LobbyResponse>> Lock(string joinCode, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EmptyLobbyRequest? request, CancellationToken cancellationToken)
    {
        if (request is null && HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true)
        {
            return LobbyProblems.Result(HttpContext, 400);
        }

        var lobby = await service.LockAsync(User, joinCode, cancellationToken);
        return lobby is null ? LobbyProblems.Result(HttpContext, 404) : Ok(lobby);
    }
}
