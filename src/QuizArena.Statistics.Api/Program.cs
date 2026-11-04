using QuizArena.Statistics.Api.Security;
using QuizArena.Statistics.Api.Services;
using QuizArena.Statistics.Data;

namespace QuizArena.Statistics.Api;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddProblemDetails();
        builder.Services.AddControllers();
        builder.Services.AddQuizArenaSecurity(builder.Configuration);
        builder.Services.AddStatisticsPersistence(builder.Configuration.GetConnectionString("DefaultConnection"));
        builder.Services.AddScoped<IStatisticsApplication, StatisticsApplication>();

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
