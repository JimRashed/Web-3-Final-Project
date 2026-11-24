using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuizArena.Identity.Api.Controllers;
using QuizArena.Identity.Domain.Exceptions;

namespace QuizArena.Identity.Api.Services;

/// <summary>
/// Maps Identity domain errors and unavailable dependencies to safe ProblemDetails. Anything unhandled falls through
/// to the default handler, which returns a generic 500 without exception details. On the picture routes every problem
/// carries the fields the contract's Problem schema requires (type, title, status, detail, traceId), so the 400 there is
/// a plain problem (no errors map) and an unexpected failure is answered here with a safe 500.
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

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        // Domain outcomes are mapped before the 503 walk: the store wraps the transient SQL error that revealed a
        // conflict, a missing profile or an unknown commit outcome, and that inner cause must not turn them into 503.
        switch (exception)
        {
            case ProfilePictureCorruptException:
                // A stored row breaks an invariant: a server-side data fault. Generic text, never the inner message.
                return await WriteAsync(httpContext, exception, UnexpectedFailure());
            case ProfilePictureCommitOutcomeUnknownException:
                // SEC-06 implementation note (#144): the write may or may not have committed; a retry may add a version.
                return await WriteAsync(httpContext, exception, new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Upload outcome unknown",
                    Detail = "The upload could not be confirmed. Reload your profile before retrying.",
                });
            case ProfilePictureConflictException:
                return await WriteAsync(httpContext, exception, new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = "The profile picture was changed by another request. Retry the upload.",
                });
            case PlayerProfileNotFoundException:
                return await WriteAsync(httpContext, exception, new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Profile not found",
                    Detail = "Save a display name to create your profile.",
                });
            case InvalidProfileException invalid when IsPictureEndpoint(httpContext):
                return await WriteAsync(httpContext, exception, new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid input",
                    Detail = invalid.Message,
                });
            case InvalidProfileException invalid:
                return await WriteAsync(httpContext, exception, new ValidationProblemDetails(
                    new Dictionary<string, string[]> { [invalid.Field] = [invalid.Message] })
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid input",
                });
        }

        if (IsDependencyUnavailable(exception))
        {
            // DR-3: a transient database failure is an outage, not a server bug (SEC-06).
            return await WriteAsync(httpContext, exception, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Dependency unavailable",
                Detail = "A required service is temporarily unavailable. Try again later.",
            });
        }

        // Elsewhere the default handler answers 500; on the picture routes the contract also requires a detail.
        return IsPictureEndpoint(httpContext) && await WriteAsync(httpContext, exception, UnexpectedFailure());
    }

    private static ProblemDetails UnexpectedFailure() => new()
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "Unexpected failure",
        Detail = "An unexpected error occurred. Try again later.",
    };

    // The exception handler middleware clears the request's endpoint but keeps it on the feature.
    private static bool IsPictureEndpoint(HttpContext httpContext) =>
        httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint?.Metadata
            .GetMetadata<PrivatePictureResponseAttribute>() is not null;

    private async ValueTask<bool> WriteAsync(HttpContext httpContext, Exception exception, ProblemDetails problem)
    {
        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    // Same transient set as the Lobby and Question Bank handlers (timeouts, network and Azure SQL throttling errors).
    // The walk stops at any Identity domain exception: its own mapping (or the generic 500) wins over a wrapped cause.
    private static bool IsDependencyUnavailable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is IdentityDomainException)
            {
                return false;
            }

            if (current is TimeoutException || current is SqlException sql && sql.Errors.Cast<SqlError>().Any(error =>
                error.Number is -2 or 2 or 53 or 64 or 233 or 4060 or 10053 or 10054 or 10060 or 11001 or 40197 or 40501 or 40613))
            {
                return true;
            }
        }

        return false;
    }
}
