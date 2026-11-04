using QuizArena.Gameplay.Api.Errors;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Data;
using QuizArena.Gameplay.Data.Repositories;
using QuizArena.Gameplay.Domain.Repositories;

namespace QuizArena.Gameplay.Api;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GameplayExceptionHandler>();
        builder.Services.AddControllers();
        builder.Services.AddQuizArenaSecurity(builder.Configuration);
        builder.Services.AddGameplayPersistence(builder.Configuration.GetConnectionString("DefaultConnection"));

        builder.Services.AddScoped<IMatchRepository, MatchRepository>();
        builder.Services.AddScoped<IGameplayApplication, GameplayApplication>();

        // Replaced by the typed HTTP adapters in the next slice of #52.
        builder.Services.AddScoped<ILobbyGateway, UnavailableLobbyGateway>();
        builder.Services.AddScoped<IQuestionCatalog, UnavailableQuestionCatalog>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseQuizArenaSecurity();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .AllowAnonymous();
        app.MapControllers();

        app.Run();
    }
}
