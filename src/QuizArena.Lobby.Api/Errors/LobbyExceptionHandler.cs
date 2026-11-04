using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using QuizArena.Lobby.Data.Repositories;
using QuizArena.Lobby.Domain.Exceptions;

namespace QuizArena.Lobby.Api.Errors;

public sealed class LobbyExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted || exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var status = exception switch
        {
            InvalidLobbyException => StatusCodes.Status400BadRequest,
            LobbyForbiddenException => StatusCodes.Status403Forbidden,
            LobbyConflictException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            LobbyPersistenceException => StatusCodes.Status503ServiceUnavailable,
            _ when IsDependencyUnavailable(exception) => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };
        httpContext.Response.StatusCode = status;
        if (status == StatusCodes.Status401Unauthorized)
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }

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
