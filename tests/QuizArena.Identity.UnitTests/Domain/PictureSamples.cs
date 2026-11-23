namespace QuizArena.Identity.UnitTests.Domain;

/// <summary>
/// Minimal PNG, JPEG and WebP byte sequences that pass the #144 header checks, plus helpers for malformed variants.
/// Duplicated in QuizArena.Identity.IntegrationTests: unit tests may reference only the Domain project.
/// </summary>
internal static class PictureSamples
{
    /// <summary>A real 1x1 RGBA PNG (signature, IHDR, IDAT, IEND).</summary>
    public static byte[] Png => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    /// <summary>SOI, APP0 (JFIF), baseline SOF0 for 1x1, EOI.</summary>
    public static byte[] Jpeg =>
    [
        0xFF, 0xD8,
        0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
        0xFF, 0xD9,
    ];

    /// <summary>SOI, APP0, DQT, fill bytes, then a progressive SOF2.</summary>
    public static byte[] JpegProgressive
    {
        get
        {
            byte[] dqt = [0xFF, 0xDB, 0x00, 0x43, 0x00, .. new byte[64]];
            byte[] sof2 = [0xFF, 0xFF, 0xC2, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xD9];
            return [.. Jpeg[..20], .. dqt, .. sof2];
        }
    }

    /// <summary>A real 1x1 lossless WebP (RIFF, WEBP, VP8L).</summary>
    public static byte[] WebpLossless => Convert.FromBase64String("UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==");

    /// <summary>RIFF/WEBP with a lossy VP8 chunk header (frame tag + 9D 01 2A start code + 1x1 size).</summary>
    public static byte[] WebpLossy =>
        Webp("VP8 "u8, [0x30, 0x01, 0x00, 0x9D, 0x01, 0x2A, 0x01, 0x00, 0x01, 0x00]);

    /// <summary>RIFF/WEBP with an extended VP8X chunk (flags + reserved + 1x1 canvas).</summary>
    public static byte[] WebpExtended =>
        Webp("VP8X"u8, [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

    /// <summary>"GIF89a" header bytes (a format the API never accepts).</summary>
    public static byte[] Gif => [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00];

    /// <summary>An SVG document (active content, never accepted).</summary>
    public static byte[] Svg => "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();

    /// <summary>Builds RIFF/WEBP around one chunk; the RIFF size and chunk size are consistent.</summary>
    public static byte[] Webp(ReadOnlySpan<byte> fourCc, byte[] payload)
    {
        var padded = payload.Length % 2 == 1 ? [.. payload, 0x00] : payload;
        var riffSize = 4 + 8 + padded.Length;
        byte[] header = [.. "RIFF"u8, .. LittleEndian(riffSize), .. "WEBP"u8, .. fourCc, .. LittleEndian(payload.Length)];
        return [.. header, .. padded];
    }

    /// <summary>The image followed by zero bytes so the result is exactly <paramref name="length"/> bytes.</summary>
    public static byte[] PaddedTo(byte[] image, int length)
    {
        var result = new byte[length];
        image.CopyTo(result, 0);
        return result;
    }

    private static byte[] LittleEndian(int value) =>
        [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
}
