using System.Text.Json.Serialization;
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
        // Unknown JSON properties are rejected with 400 instead of being silently ignored.
        builder.Services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
        builder.Services.AddExceptionHandler<StatisticsExceptionHandler>();
        builder.Services.AddOpenApi();
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
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.Run();
    }
}
