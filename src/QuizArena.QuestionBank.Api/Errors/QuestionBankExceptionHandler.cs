using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using QuizArena.QuestionBank.Domain.Exceptions;

namespace QuizArena.QuestionBank.Api.Errors;

public sealed class QuestionBankExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted || exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var status = exception switch
        {
            InvalidQuestionCountException => StatusCodes.Status400BadRequest,
            InsufficientQuestionCatalogException => StatusCodes.Status409Conflict,
            _ when IsDependencyUnavailable(exception) => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };
        httpContext.Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status },
        });
        return true;
    }

    private static bool IsDependencyUnavailable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException || current is SqlException sql && sql.Errors.Cast<SqlError>().Any(error =>
                error.Number is -2 or 2 or 53 or 64 or 233 or 4060 or 10053 or 10054 or 10060 or 11001 or 40197 or 40501 or 40613))
            {
                return true;
            }
        }

        return false;
    }
}
