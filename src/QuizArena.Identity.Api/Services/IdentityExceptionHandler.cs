using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuizArena.Identity.Domain.Exceptions;

namespace QuizArena.Identity.Api.Services;

/// <summary>
/// Maps Identity domain errors and unavailable dependencies to safe ProblemDetails. Anything unhandled falls through
/// to the default handler, which returns a generic 500 without exception details.
/// </summary>
public sealed class IdentityExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The caller went away; do not report a server error for a cancelled request.
            return true;
        }

        if (exception is InvalidProfileException invalid)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return await problems.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ValidationProblemDetails(new Dictionary<string, string[]> { [invalid.Field] = [invalid.Message] })
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid input",
                },
            });
        }

        if (!httpContext.Response.HasStarted && IsDependencyUnavailable(exception))
        {
            // DR-3: a transient database failure is an outage, not a server bug (SEC-06).
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return await problems.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Dependency unavailable",
                },
            });
        }

        return false;
    }

    // Same transient set as the Lobby and Question Bank handlers (timeouts, network and Azure SQL throttling errors).
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
