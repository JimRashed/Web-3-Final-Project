using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Domain.Exceptions;

namespace QuizArena.Gameplay.Api.Errors;

/// <summary>
/// Maps gameplay errors to ProblemDetails: request decisions from the application service keep their
/// status, domain conflicts become 409, invalid input becomes 400 and anything else falls through to
/// the generic 500 without leaking exception details.
/// </summary>
public sealed class GameplayExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var status = exception switch
        {
            GameplayRequestException request => request.StatusCode,
            MatchAccessDeniedException => StatusCodes.Status403Forbidden,
            MatchConflictException => StatusCodes.Status409Conflict,
            InvalidMatchException => StatusCodes.Status400BadRequest,
            _ => 0,
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status },
        });

        return true;
    }
}
