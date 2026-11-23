using FluentAssertions;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Exceptions;
using Xunit;

namespace QuizArena.Identity.UnitTests.Domain;

public sealed class ProfilePictureFormatTests
{
    public static TheoryData<string, string> ValidImages => new()
    {
        { "png", "image/png" },
        { "jpeg-baseline", "image/jpeg" },
        { "jpeg-progressive", "image/jpeg" },
        { "webp-vp8", "image/webp" },
        { "webp-vp8l", "image/webp" },
        { "webp-vp8x", "image/webp" },
    };

    [Theory]
    [MemberData(nameof(ValidImages))]
    public void Validate_WithMinimalValidImage_DoesNotThrow(string sample, string contentType)
    {
        // Arrange
        var content = Sample(sample);

        // Act
        var act = () => ProfilePictureFormat.Validate(contentType, content);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithTrailingBytesUpToTheLimit_DoesNotThrow()
    {
        // Act
        var act = () => ProfilePictureFormat.Validate("image/png", PictureSamples.PaddedTo(PictureSamples.Png, ProfilePicture.MaxContentLength));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithEmptyContent_ThrowsContentRequired()
    {
        // Act
        var act = () => ProfilePictureFormat.Validate("image/png", []);

        // Assert
        act.Should().Throw<InvalidProfileException>()
            .Where(e => e.Field == "content" && e.Message == "Picture content is required.");
    }

    [Theory]
    [InlineData("gif")]
    [InlineData("svg")]
    [InlineData("bmp")]
    [InlineData("random")]
    public void Validate_WithUnknownMagic_Throws(string sample)
    {
        // Arrange
        var content = sample switch
        {
            "gif" => PictureSamples.Gif,
            "svg" => PictureSamples.Svg,
            "bmp" => [0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x36, 0x00],
            _ => [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C],
        };

        // Act
        var act = () => ProfilePictureFormat.Validate("image/png", content);

        // Assert
        act.Should().Throw<InvalidProfileException>()
            .Where(e => e.Field == "content" && e.Message == "Picture content is not a PNG, JPEG or WebP image.");
    }

    [Theory]
    [InlineData("png", "image/jpeg")]
    [InlineData("png", "image/webp")]
    [InlineData("jpeg-baseline", "image/png")]
    [InlineData("webp-vp8l", "image/png")]
    [InlineData("webp-vp8", "image/jpeg")]
    public void Validate_WithMismatchedContentType_Throws(string sample, string contentType)
    {
        // Act
        var act = () => ProfilePictureFormat.Validate(contentType, Sample(sample));

        // Assert
        act.Should().Throw<InvalidProfileException>()
            .Where(e => e.Field == "content" && e.Message == "Picture content does not match the Content-Type.");
    }

    [Theory]
    [InlineData("signature-only")]
    [InlineData("32-bytes")]
    [InlineData("ihdr-length")]
    [InlineData("chunk-type")]
    public void Validate_WithTruncatedPng_Throws(string variant)
    {
        // Arrange
        var content = PictureSamples.Png;
        content = variant switch
        {
            "signature-only" => content[..8],
            "32-bytes" => content[..32],
            "ihdr-length" => With(content, 11, 0x0C),
            _ => With(content, 12, (byte)'i'),
        };

        // Act / Assert
        AssertMalformed("image/png", content);
    }

    [Theory]
    [InlineData("soi-only")]
    [InlineData("sos-before-sof")]
    [InlineData("eoi-before-sof")]
    [InlineData("length-past-end")]
    [InlineData("length-below-two")]
    [InlineData("sof-too-short")]
    [InlineData("non-ff-marker")]
    [InlineData("dht-not-sof")]
    [InlineData("jpg-not-sof")]
    [InlineData("dac-not-sof")]
    public void Validate_WithMalformedJpeg_Throws(string variant)
    {
        // Arrange
        byte[] soiApp0 = PictureSamples.Jpeg[..20];
        byte[] content = variant switch
        {
            "soi-only" => [0xFF, 0xD8, 0xFF],
            "sos-before-sof" => [.. soiApp0, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00],
            "eoi-before-sof" => [.. soiApp0, 0xFF, 0xD9],
            "length-past-end" => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x40, 0x4A, 0x46],
            "length-below-two" => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x01, 0x4A, 0x46],
            "sof-too-short" => [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x06, 0x08, 0x00, 0x01, 0x00],
            "non-ff-marker" => [.. soiApp0, 0x12, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00],
            "dht-not-sof" => [.. soiApp0, 0xFF, 0xC4, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00],
            "jpg-not-sof" => [.. soiApp0, 0xFF, 0xC8, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00],
            _ => [.. soiApp0, 0xFF, 0xCC, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00],
        };

        // Act / Assert
        AssertMalformed("image/jpeg", content);
    }

    [Theory]
    [InlineData("riff-size-past-end")]
    [InlineData("header-only")]
    [InlineData("unknown-chunk")]
    [InlineData("chunk-size-past-end")]
    [InlineData("vp8-missing-start-code")]
    [InlineData("vp8-too-short")]
    [InlineData("vp8l-wrong-signature")]
    [InlineData("vp8x-too-short")]
    [InlineData("chunk-size-past-riff-end")]
    [InlineData("missing-pad-byte")]
    public void Validate_WithMalformedWebp_Throws(string variant)
    {
        // Arrange
        var content = variant switch
        {
            "riff-size-past-end" => With(PictureSamples.WebpLossless, 4, 0x40),
            "header-only" => PictureSamples.WebpLossless[..12],
            "unknown-chunk" => PictureSamples.Webp("ALPH"u8, new byte[10]),
            "chunk-size-past-end" => With(PictureSamples.WebpLossless, 16, 0x30),
            "vp8-missing-start-code" => PictureSamples.Webp("VP8 "u8, [0x30, 0x01, 0x00, 0x9D, 0x01, 0x2B, 0x01, 0x00, 0x01, 0x00]),
            "vp8-too-short" => PictureSamples.Webp("VP8 "u8, [0x30, 0x01, 0x00, 0x9D, 0x01, 0x2A]),
            "vp8l-wrong-signature" => With(PictureSamples.WebpLossless, 20, 0x2E),
            "vp8x-too-short" => PictureSamples.Webp("VP8X"u8, new byte[9]),
            "chunk-size-past-riff-end" => ChunkPastRiffEnd(),
            _ => MissingPadByte(),
        };

        // Act / Assert
        AssertMalformed("image/webp", content);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/JPEG")]
    [InlineData("image/svg+xml")]
    [InlineData("")]
    public void Validate_WithUnsupportedContentType_Throws(string contentType)
    {
        // Act
        var act = () => ProfilePictureFormat.Validate(contentType, PictureSamples.Jpeg);

        // Assert
        act.Should().Throw<InvalidProfileException>().Where(e => e.Field == "contentType");
    }

    [Fact]
    public void Validate_FailureMessage_DoesNotEchoContent()
    {
        // Arrange
        var content = "<svg>SECRET-PAYLOAD-MARKER</svg>"u8.ToArray();

        // Act
        var act = () => ProfilePictureFormat.Validate("image/png", content);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Message.Should().NotContain("SECRET").And.NotContain("svg");
    }

    private static byte[] Sample(string name) => name switch
    {
        "png" => PictureSamples.Png,
        "jpeg-baseline" => PictureSamples.Jpeg,
        "jpeg-progressive" => PictureSamples.JpegProgressive,
        "webp-vp8" => PictureSamples.WebpLossy,
        "webp-vp8l" => PictureSamples.WebpLossless,
        "webp-vp8x" => PictureSamples.WebpExtended,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    // The VP8L chunk claims 23 bytes: within the HTTP body (trailing zeros) but past the RIFF end (34).
    private static byte[] ChunkPastRiffEnd()
    {
        var bytes = PictureSamples.PaddedTo(PictureSamples.WebpLossless, PictureSamples.WebpLossless.Length + 20);
        bytes[16] = 23;
        return bytes;
    }

    // The 13-byte VP8L chunk needs a pad byte; the RIFF size (25) ends right after the payload, without it.
    private static byte[] MissingPadByte()
    {
        var bytes = PictureSamples.WebpLossless[..33];
        bytes[4] = 25;
        return bytes;
    }

    private static byte[] With(byte[] content, int index, byte value)
    {
        var copy = (byte[])content.Clone();
        copy[index] = value;
        return copy;
    }

    private static void AssertMalformed(string contentType, byte[] content)
    {
        var act = () => ProfilePictureFormat.Validate(contentType, content);
        act.Should().Throw<InvalidProfileException>()
            .Where(e => e.Field == "content" && e.Message == "Picture content is malformed or truncated.");
    }
}
