using Microsoft.EntityFrameworkCore;
using QuizArena.Data.Context;
using QuizArena.Domain.Lobby.Entities;

namespace QuizArena.Data.Repositories;

public sealed class LobbyRepository(QuizArenaContext context) : ILobbyRepository
{
    public Task<ActiveLobby?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default)
    {
        return context.ActiveLobbies
            .Include(lobby => lobby.Players)
            .SingleOrDefaultAsync(lobby => lobby.JoinCode == joinCode, cancellationToken);
    }

    public async Task SaveAsync(ActiveLobby lobby, CancellationToken cancellationToken = default)
    {
        context.ActiveLobbies.Update(lobby);
        await context.SaveChangesAsync(cancellationToken);
    }
}
