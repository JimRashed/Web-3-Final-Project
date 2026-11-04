namespace QuizArena.Lobby.Data.Repositories;

public sealed class LobbyPersistenceException(string message, Exception innerException) : Exception(message, innerException);
