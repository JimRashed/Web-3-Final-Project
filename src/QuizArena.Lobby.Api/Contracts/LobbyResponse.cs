namespace QuizArena.Lobby.Api.Contracts;

public sealed record LobbyResponse(Guid LobbyId, string JoinCode, string Status, int MemberCount, bool IsMember, bool IsHost);
