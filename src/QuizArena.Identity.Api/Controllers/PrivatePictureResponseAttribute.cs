namespace QuizArena.Identity.Api.Controllers;

/// <summary>
/// Marks an endpoint whose every response (success, rejection or failure) must carry
/// <c>Cache-Control: private, no-store</c> and <c>X-Content-Type-Options: nosniff</c> (SEC-01, SEC-08).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class PrivatePictureResponseAttribute : Attribute
{
}

internal static class PrivatePictureResponseExtensions
{
    /// <summary>
    /// Register right after <c>UseExceptionHandler</c>: response-starting callbacks run in reverse order of registration,
    /// so these headers are applied after the exception handler's own cache-header reset on 500/503 responses.
    /// </summary>
    public static IApplicationBuilder UsePrivatePictureHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<PrivatePictureResponseAttribute>() is not null)
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "private, no-store";
                    context.Response.Headers.XContentTypeOptions = "nosniff";
                    return Task.CompletedTask;
                });
            }

            return next(context);
        });
}
