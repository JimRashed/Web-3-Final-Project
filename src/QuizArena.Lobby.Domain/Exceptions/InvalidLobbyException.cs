namespace QuizArena.Lobby.Domain.Exceptions;

public sealed class InvalidLobbyException : LobbyDomainException
{
    public InvalidLobbyException(string field, string message)
        : base(message)
    {
        Field = field;
    }

    public string Field { get; }
}
