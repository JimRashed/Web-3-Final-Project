using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace QuizArena.Identity.Api.Controllers;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequireJsonContentTypeAttribute : Attribute, IResourceFilter, IApiRequestMetadataProvider
{
    private const string JsonMediaType = "application/json";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        // Check the media type after authorization, before MVC deserializes the request.
        var contentType = context.HttpContext.Request.ContentType;
        if (!string.IsNullOrEmpty(contentType) &&
            (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType) ||
             !string.Equals(mediaType.MediaType.Value, JsonMediaType, StringComparison.OrdinalIgnoreCase)))
        {
            context.Result = new UnsupportedMediaTypeResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }

    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
        contentTypes.Clear();
        contentTypes.Add(JsonMediaType);
    }
}
