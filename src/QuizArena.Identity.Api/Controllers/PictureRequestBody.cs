namespace QuizArena.Identity.Api.Controllers;

/// <summary>Reads a raw picture body without ever buffering more than <c>limit + 1</c> bytes (SEC-05, 413).</summary>
internal static class PictureRequestBody
{
    /// <summary>
    /// The exact body bytes, or null when the body is larger than <paramref name="limit"/>. A declared Content-Length
    /// above the limit is rejected without reading; chunked bodies are read until one byte past the limit at most.
    /// </summary>
    public static async Task<byte[]?> ReadAsync(HttpRequest request, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentLength > limit)
        {
            return null;
        }

        var capacity = (int)Math.Min(request.ContentLength ?? 81_920L, limit + 1L);
        using var buffer = new MemoryStream(capacity);
        var chunk = new byte[81_920];
        var cap = limit + 1;
        while (buffer.Length < cap)
        {
            var toRead = (int)Math.Min(chunk.Length, cap - buffer.Length);
            var read = await request.Body.ReadAsync(chunk.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.Length > limit ? null : buffer.ToArray();
    }
}
