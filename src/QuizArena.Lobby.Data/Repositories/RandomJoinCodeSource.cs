using QuizArena.Lobby.Domain.Services;

namespace QuizArena.Lobby.Data.Repositories;

public sealed class RandomJoinCodeSource : IJoinCodeSource
{
    public string Next() => JoinCodeGenerator.Generate();
}
