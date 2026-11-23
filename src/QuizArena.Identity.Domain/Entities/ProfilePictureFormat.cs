using QuizArena.Identity.Domain.Exceptions;

namespace QuizArena.Identity.Domain.Entities;

/// <summary>
/// Header-level checks that uploaded bytes really are the declared PNG, JPEG or WebP image (SEC-05, #144).
/// It inspects signatures and the first structural chunk only: no CRC, pixel dimensions or decoding. Kept apart from
/// <see cref="ProfilePicture"/> so rehydrating stored rows never depends on these upload checks.
/// </summary>
public static class ProfilePictureFormat
{
    private const string ContentField = "content";

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Throws <see cref="InvalidProfileException"/> when <paramref name="content"/> is empty, is not a PNG, JPEG or WebP
    /// image, does not match <paramref name="contentType"/> (exact, lowercase) or has a malformed header.
    /// </summary>
    public static void Validate(string contentType, ReadOnlySpan<byte> content)
    {
        if (contentType is null || !ProfilePicture.AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidProfileException("contentType", "Picture content type must be image/png, image/jpeg or image/webp.");
        }

        if (content.IsEmpty)
        {
            throw new InvalidProfileException(ContentField, "Picture content is required.");
        }

        var detected = Detect(content)
            ?? throw new InvalidProfileException(ContentField, "Picture content is not a PNG, JPEG or WebP image.");
        if (!string.Equals(detected, contentType, StringComparison.Ordinal))
        {
            throw new InvalidProfileException(ContentField, "Picture content does not match the Content-Type.");
        }

        var wellFormed = detected switch
        {
            "image/png" => IsPng(content),
            "image/jpeg" => IsJpeg(content),
            _ => IsWebp(content),
        };
        if (!wellFormed)
        {
            throw new InvalidProfileException(ContentField, "Picture content is malformed or truncated.");
        }
    }

    private static string? Detect(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(PngSignature))
        {
            return "image/png";
        }

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }

    // Signature, then the IHDR chunk: 4-byte length (13), "IHDR", 13 data bytes, 4-byte CRC (not verified) = 33 bytes.
    private static bool IsPng(ReadOnlySpan<byte> content) =>
        content.Length >= 33
        && ReadUInt32BigEndian(content, 8) == 13
        && content.Slice(12, 4).SequenceEqual("IHDR"u8);

    // Walks the marker segments after SOI until a start-of-frame segment is found.
    private static bool IsJpeg(ReadOnlySpan<byte> content)
    {
        var offset = 2;
        while (offset < content.Length)
        {
            if (content[offset] != 0xFF)
            {
                return false;
            }

            // Skip fill bytes (any number of 0xFF before the marker code).
            while (offset < content.Length && content[offset] == 0xFF)
            {
                offset++;
            }

            if (offset >= content.Length)
            {
                return false;
            }

            var marker = content[offset++];
            if (marker is >= 0xD0 and <= 0xD7 or 0x01)
            {
                continue; // Standalone markers carry no length.
            }

            if (marker is 0xDA or 0xD9 or 0xD8 or 0x00)
            {
                return false; // Scan data, end of image, a second SOI or a stuffed byte before any frame header.
            }

            if (offset + 2 > content.Length)
            {
                return false;
            }

            var length = (content[offset] << 8) | content[offset + 1];
            if (length < 2 || offset + length > content.Length)
            {
                return false;
            }

            if (IsStartOfFrame(marker))
            {
                return length >= 8;
            }

            offset += length;
        }

        return false;
    }

    // SOF0-SOF15 except DHT (C4), JPG (C8) and DAC (CC).
    private static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;

    // RIFF header (size covers everything after the first 8 bytes), "WEBP", then the first chunk: VP8, VP8L or VP8X.
    // The chunk header, payload and its pad byte (odd sizes are padded to even) must end within the RIFF end
    // (RIFF size + 8), which itself must lie within the body; bytes after the RIFF end are never treated as image data.
    private static bool IsWebp(ReadOnlySpan<byte> content)
    {
        var riffEnd = ReadUInt32LittleEndian(content, 4) + 8L;
        if (riffEnd > content.Length || riffEnd < 20)
        {
            return false;
        }

        var chunkSize = ReadUInt32LittleEndian(content, 16);
        var paddedSize = chunkSize + (chunkSize & 1L);
        if (20L + paddedSize > riffEnd)
        {
            return false;
        }

        var fourCc = content.Slice(12, 4);
        var payload = content.Slice(20, (int)chunkSize);
        if (fourCc.SequenceEqual("VP8 "u8))
        {
            return payload.Length >= 10 && payload[3] == 0x9D && payload[4] == 0x01 && payload[5] == 0x2A;
        }

        if (fourCc.SequenceEqual("VP8L"u8))
        {
            return payload.Length >= 5 && payload[0] == 0x2F;
        }

        return fourCc.SequenceEqual("VP8X"u8) && payload.Length >= 10;
    }

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> content, int offset) =>
        ((uint)content[offset] << 24) | ((uint)content[offset + 1] << 16) | ((uint)content[offset + 2] << 8) | content[offset + 3];

    private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> content, int offset) =>
        content[offset] | ((uint)content[offset + 1] << 8) | ((uint)content[offset + 2] << 16) | ((uint)content[offset + 3] << 24);
}
