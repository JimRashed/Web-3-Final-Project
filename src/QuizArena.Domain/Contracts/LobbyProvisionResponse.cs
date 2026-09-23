namespace QuizArena.Domain.Contracts;

public sealed record LobbyProvisionResponse(Guid LobbyId, string JoinCode, string Status, IReadOnlyList<string> PlayerIds);
