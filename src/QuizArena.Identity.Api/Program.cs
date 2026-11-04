using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Api.Services;
using QuizArena.Identity.Data;

namespace QuizArena.Identity.Api;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddProblemDetails();
        builder.Services.AddControllers();
        builder.Services.AddQuizArenaSecurity(builder.Configuration);
        builder.Services.AddIdentityPersistence(builder.Configuration.GetConnectionString("DefaultConnection"));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IProfileApplication, ProfileApplication>();

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
