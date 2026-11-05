using System.Text.Json.Serialization;
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
        // Unknown JSON properties (e.g. a spoofed userId) are rejected with 400.
        builder.Services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
        builder.Services.AddExceptionHandler<IdentityExceptionHandler>();
        builder.Services.AddOpenApi();
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
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.Run();
    }
}
