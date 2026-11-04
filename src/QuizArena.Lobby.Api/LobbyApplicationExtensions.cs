using QuizArena.Lobby.Api.Services;

namespace QuizArena.Lobby.Api;

public static class LobbyApplicationExtensions
{
    public static IServiceCollection AddLobbyApplication(this IServiceCollection services)
    {
        services.AddScoped<ILobbyService, LobbyService>();
        return services;
    }
}
