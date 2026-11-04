using QuizArena.Notification.Api.Security;
using QuizArena.Notification.Api.Services;
using QuizArena.Notification.Data;
using QuizArena.Notification.Data.Repositories;
using QuizArena.Notification.Domain.Repositories;

namespace QuizArena.Notification.Api;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<Errors.NotificationExceptionHandler>();
        builder.Services.AddControllers();
        builder.Services.AddQuizArenaSecurity(builder.Configuration);
        builder.Services.AddNotificationPersistence(builder.Configuration.GetConnectionString("DefaultConnection"));

        builder.Services.AddScoped<IEventRepository, EventRepository>();
        builder.Services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
        builder.Services.AddScoped<IEventApplication, EventApplication>();

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
