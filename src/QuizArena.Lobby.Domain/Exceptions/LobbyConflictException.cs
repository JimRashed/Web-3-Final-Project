namespace QuizArena.Lobby.Domain.Exceptions;

public sealed class LobbyConflictException : LobbyDomainException
{
    public LobbyConflictException(string message)
        : base(message)
    {
    }
}
