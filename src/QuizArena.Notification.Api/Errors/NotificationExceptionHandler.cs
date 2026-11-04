using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Notification.Domain.Exceptions;

namespace QuizArena.Notification.Api.Errors;

/// <summary>
/// Maps notification domain errors to ProblemDetails. Anything unrecognised falls through to the
/// default handler, which answers a generic 500 without exception details.
/// </summary>
public sealed class NotificationExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var status = exception switch
        {
            EventConflictException => StatusCodes.Status409Conflict,
            InvalidEventException => StatusCodes.Status400BadRequest,
            NotificationDomainException => StatusCodes.Status400BadRequest,
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
            ProblemDetails = { Status = status, Title = "The notification request is not valid." },
        });

        return true;
    }
}
