namespace QuizArena.Lobby.Domain.Exceptions;

public class LobbyDomainException : Exception
{
    public LobbyDomainException(string message)
        : base(message)
    {
    }
}
