using System.Text.Json.Serialization;

namespace QuizArena.Lobby.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmptyLobbyRequest;
