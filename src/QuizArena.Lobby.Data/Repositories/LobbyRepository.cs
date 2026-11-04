using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Lobby.Data.Context;
using QuizArena.Lobby.Data.Entities;
using QuizArena.Lobby.Domain.Entities;
using QuizArena.Lobby.Domain.Services;

namespace QuizArena.Lobby.Data.Repositories;

public sealed class LobbyRepository(IDbContextFactory<LobbyDbContext> contextFactory, IJoinCodeSource codeSource) : ILobbyRepository
{
    private const int CreateAttempts = 5;
    private const int MutationAttempts = 3;

    public async Task<ActiveLobby> CreateAsync(string hostId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lobby = ActiveLobby.Create(hostId, codeSource.Next());
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            context.ActiveLobbies.Add(ToRow(lobby));
            try
            {
                // EF atomically inserts the root and its host membership in one SaveChanges transaction.
                await context.SaveChangesAsync(cancellationToken);
                return lobby;
            }
            catch (DbUpdateException exception) when (IsUniqueIndexViolation(exception, "IX_ActiveLobbies_JoinCode"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (attempt == CreateAttempts)
                {
                    throw new LobbyPersistenceException("A lobby join code could not be allocated. Please retry.", exception);
                }
            }
        }
    }

    public async Task<ActiveLobby?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JoinCodeGenerator.Validate(joinCode);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.ActiveLobbies.AsNoTracking().Include(lobby => lobby.Players)
            .SingleOrDefaultAsync(lobby => lobby.JoinCode == joinCode, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public Task<ActiveLobby?> JoinAsync(string joinCode, string playerId, CancellationToken cancellationToken = default) =>
        SaveAsync(joinCode, lobby => lobby.Join(playerId), cancellationToken);

    public async Task<ActiveLobby?> SaveAsync(string joinCode, Func<ActiveLobby, bool> mutation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(mutation);
        JoinCodeGenerator.Validate(joinCode);
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var row = await context.ActiveLobbies.Include(lobby => lobby.Players)
                .SingleOrDefaultAsync(lobby => lobby.JoinCode == joinCode, cancellationToken);
            if (row is null)
            {
                return null;
            }

            var lobby = ToDomain(row);
            if (!mutation(lobby))
            {
                return lobby;
            }

            row.Status = lobby.Status;
            // A membership-only change must check and advance the parent's original rowversion too.
            row.WriteRevision = checked(row.WriteRevision + 1);
            var existingIds = row.Players.Select(player => player.Id).ToHashSet();
            foreach (var player in lobby.Players.Where(player => !existingIds.Contains(player.Id)))
            {
                // Domain assigns the Guid; mark it Added explicitly rather than inferring an existing row.
                context.LobbyPlayers.Add(ToRow(player));
            }

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return lobby;
            }
            catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException
                || IsUniqueIndexViolation(exception, "IX_LobbyPlayers_LobbyId_PlayerId"))
            {
                // Roll back any membership inserted before the stale-parent update was detected.
                await transaction.RollbackAsync(CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
                if (attempt == MutationAttempts)
                {
                    throw new LobbyPersistenceException("The lobby changed repeatedly. Please retry.", exception);
                }
            }
        }
    }

    private static ActiveLobby ToDomain(ActiveLobbyRow row) => ActiveLobby.Rehydrate(
        row.Id, row.JoinCode, row.HostId, row.Status,
        row.Players.OrderBy(player => player.Id).Select(player => LobbyPlayer.Rehydrate(player.Id, player.LobbyId, player.PlayerId)));

    private static ActiveLobbyRow ToRow(ActiveLobby lobby) => new()
    {
        Id = lobby.Id,
        JoinCode = lobby.JoinCode,
        HostId = lobby.HostId,
        Status = lobby.Status,
        Players = lobby.Players.Select(ToRow).ToList(),
    };

    private static LobbyPlayerRow ToRow(LobbyPlayer player) => new() { Id = player.Id, LobbyId = player.LobbyId, PlayerId = player.PlayerId };

    private static bool IsUniqueIndexViolation(DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException sqlException
        && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627
            && error.Message.Contains($"'{indexName}'", StringComparison.Ordinal));
}
