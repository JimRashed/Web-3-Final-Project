using QuizArena.Gameplay.Domain.Entities;

namespace QuizArena.Gameplay.Api.Services;

/// <summary>
/// Placeholder adapters used until the typed HTTP clients for Lobby and QuestionBank land (next
/// slice of #52). They fail closed with 503 — a match is never created from partial information.
/// </summary>
public sealed class UnavailableLobbyGateway : ILobbyGateway
{
    public Task<LobbyState?> GetAsync(string joinCode, CancellationToken cancellationToken) =>
        throw new GameplayRequestException(StatusCodes.Status503ServiceUnavailable, "The lobby service is not wired yet.");
}

/// <inheritdoc cref="UnavailableLobbyGateway"/>
public sealed class UnavailableQuestionCatalog : IQuestionCatalog
{
    public Task<IReadOnlyList<QuestionSnapshot>> SnapshotAsync(int count, CancellationToken cancellationToken) =>
        throw new GameplayRequestException(StatusCodes.Status503ServiceUnavailable, "The question bank is not wired yet.");
}
