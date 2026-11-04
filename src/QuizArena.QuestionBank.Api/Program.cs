using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.Data;

namespace QuizArena.QuestionBank.Api;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddProblemDetails();
        builder.Services.AddControllers();
        builder.Services.AddQuizArenaSecurity(builder.Configuration);
        builder.Services.AddQuestionBankPersistence(builder.Configuration.GetConnectionString("DefaultConnection"));

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
