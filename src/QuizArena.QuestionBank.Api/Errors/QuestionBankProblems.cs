using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace QuizArena.QuestionBank.Api.Errors;

public static class QuestionBankProblems
{
    private const int MaxTraceIdLength = 100;

    public static void Customize(ProblemDetailsContext context) => Apply(context.ProblemDetails, context.HttpContext);

    public static ObjectResult Result(HttpContext context, int status)
    {
        var problem = new ProblemDetails { Status = status };
        Apply(problem, context);
        return new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { "application/problem+json" } };
    }

    private static void Apply(ProblemDetails problem, HttpContext context)
    {
        var status = problem.Status ?? context.Response.StatusCode;
        // The question-set contract treats body binding failures as invalid input.
        if (status == StatusCodes.Status415UnsupportedMediaType)
        {
            status = StatusCodes.Status400BadRequest;
            context.Response.StatusCode = status;
        }

        var description = status switch
        {
            400 => "Invalid input",
            401 => "Missing or invalid bearer token",
            403 => "Insufficient scope, actor kind or ownership",
            409 => "Insufficient valid question catalog",
            503 => "Dependency unavailable",
            _ => "The request could not be completed",
        };
        problem.Type = $"https://quizarena.example/problems/{status}";
        problem.Title = description;
        problem.Status = status;
        problem.Detail = description;
        problem.Instance = null;
        problem.Extensions.Clear();
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        problem.Extensions["traceId"] = string.IsNullOrWhiteSpace(traceId)
            ? Guid.NewGuid().ToString("N") : traceId[..Math.Min(traceId.Length, MaxTraceIdLength)];
    }
}
