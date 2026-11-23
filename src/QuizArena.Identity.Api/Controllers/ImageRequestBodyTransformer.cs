using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace QuizArena.Identity.Api.Controllers;

/// <summary>
/// Documents the raw image request body of actions marked with <see cref="RequireImageContentTypeAttribute"/>.
/// Those actions read <c>Request.Body</c> themselves (no [FromBody], no [Consumes]: DR-9), so the generator would
/// otherwise emit no requestBody. Documentation only; it does not change request handling or auth precedence.
/// </summary>
internal sealed class ImageRequestBodyTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<RequireImageContentTypeAttribute>().Any())
        {
            return Task.CompletedTask;
        }

        var content = new Dictionary<string, OpenApiMediaType>();
        foreach (var mediaType in RequireImageContentTypeAttribute.ImageMediaTypes)
        {
            content[mediaType] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
            };
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "Raw image bytes, at most 2,097,152 bytes (2 MiB). Magic bytes must match the Content-Type.",
            Content = content,
        };
        return Task.CompletedTask;
    }
}
