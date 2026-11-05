using Microsoft.AspNetCore.Mvc;
using QuizArena.Lobby.Api.Errors;
using QuizArena.Lobby.Api.Security;
using QuizArena.Lobby.Data;

namespace QuizArena.Lobby.Api;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = LobbyProblems.Customize);
        builder.Services.AddExceptionHandler<LobbyExceptionHandler>();
        builder.Services.AddControllers();
        builder.Services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = context => LobbyProblems.Result(context.HttpContext, 400));
        builder.Services.AddQuizArenaSecurity(builder.Configuration);
        builder.Services.AddLobbyPersistence(builder.Configuration.GetConnectionString("DefaultConnection"));
        builder.Services.AddLobbyApplication();

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
