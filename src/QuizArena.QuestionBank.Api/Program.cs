using Microsoft.AspNetCore.Mvc;
using QuizArena.QuestionBank.Api.Errors;
using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.Api.Services;
using QuizArena.QuestionBank.Data;

namespace QuizArena.QuestionBank.Api;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = QuestionBankProblems.Customize);
        builder.Services.AddExceptionHandler<QuestionBankExceptionHandler>();
        builder.Services.AddControllers();
        builder.Services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = context => QuestionBankProblems.Result(context.HttpContext, 400));
        builder.Services.AddQuizArenaSecurity(builder.Configuration);
        builder.Services.AddQuestionBankPersistence(builder.Configuration.GetConnectionString("DefaultConnection"));
        builder.Services.AddScoped<IQuestionCatalogService, QuestionCatalogService>();

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
