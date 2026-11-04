using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Statistics.Domain.Exceptions;

namespace QuizArena.Statistics.Api.Services;

/// <summary>
/// Maps Statistics errors to safe ProblemDetails: invalid input 400, another player's data 403, conflicting
/// result 409. Anything else falls through to the default handler's generic 500.
/// </summary>
public sealed class StatisticsExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The caller went away; do not report a server error for a cancelled request.
            return true;
        }

        ProblemDetails? problem = exception switch
        {
            InvalidMatchResultException invalid => new ValidationProblemDetails(
                new Dictionary<string, string[]> { [invalid.Field] = [invalid.Message] })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid input",
            },
            ForbiddenAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Insufficient scope, actor kind or ownership",
                Detail = exception.Message,
            },
            ResultConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicting duplicate or state",
                Detail = exception.Message,
            },
            _ => null,
        };
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}
