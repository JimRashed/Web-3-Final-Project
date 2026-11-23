using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace QuizArena.Identity.Api.Controllers;

/// <summary>
/// 415 for anything but a PNG, JPEG or WebP Content-Type. A resource filter runs after authorization, so a caller
/// without the grant gets 401/403 and never 415 (DR-9: no [Consumes], which would answer 415 before authorization).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequireImageContentTypeAttribute : Attribute, IResourceFilter, IApiRequestMetadataProvider
{
    internal static readonly string[] ImageMediaTypes = ["image/png", "image/jpeg", "image/webp"];

    /// <summary>
    /// Parses a Content-Type header; parameters are ignored and the comparison is case-insensitive. On success
    /// <paramref name="mediaType"/> is the canonical lowercase type (image/png, image/jpeg or image/webp).
    /// </summary>
    public static bool TryGetImageMediaType(string? header, [NotNullWhen(true)] out string? mediaType)
    {
        mediaType = null;
        if (string.IsNullOrEmpty(header) || !MediaTypeHeaderValue.TryParse(header, out var parsed))
        {
            return false;
        }

        foreach (var candidate in ImageMediaTypes)
        {
            if (string.Equals(parsed.MediaType.Value, candidate, StringComparison.OrdinalIgnoreCase))
            {
                mediaType = candidate;
                return true;
            }
        }

        return false;
    }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!TryGetImageMediaType(context.HttpContext.Request.ContentType, out _))
        {
            context.Result = UnsupportedMediaType(context.HttpContext);
        }
    }

    /// <summary>A 415 problem with every field the contract's Problem schema requires (type, title, status, detail, traceId).</summary>
    public static ObjectResult UnsupportedMediaType(HttpContext httpContext)
    {
        var problem = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>().CreateProblemDetails(
            httpContext,
            StatusCodes.Status415UnsupportedMediaType,
            title: "Unsupported media type",
            detail: "Only image/png, image/jpeg and image/webp are accepted.");
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status415UnsupportedMediaType,
            ContentTypes = { "application/problem+json" },
        };
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }

    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
        contentTypes.Clear();
        foreach (var mediaType in ImageMediaTypes)
        {
            contentTypes.Add(mediaType);
        }
    }
}
