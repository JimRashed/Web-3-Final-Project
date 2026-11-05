namespace QuizArena.Lobby.Domain.Exceptions;

public sealed class LobbyForbiddenException : LobbyDomainException
{
    public LobbyForbiddenException(string message)
        : base(message)
    {
    }
}
