using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Identity.Domain.Exceptions;

namespace QuizArena.Identity.Api.Services;

/// <summary>
/// Maps Identity domain errors to safe ProblemDetails. Anything unhandled falls through to the default
/// handler, which returns a generic 500 without exception details.
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

        if (exception is not InvalidProfileException invalid)
        {
            return false;
        }

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
}
