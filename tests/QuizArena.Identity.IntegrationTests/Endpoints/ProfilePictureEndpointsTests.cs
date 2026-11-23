using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Endpoints;

/// <summary>
/// HTTP tests for GET/PUT /api/users/me/picture (#144) through the real pipeline and database: validated uploads,
/// owner-only reads, the 415 → 413 → 400 → 404 check order, contract problem bodies and private response headers.
/// </summary>
public sealed class ProfilePictureEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private const string PicturePath = "/api/users/me/picture";
    private const int Limit = ProfilePicture.MaxContentLength;
    private readonly IdentityApiFactory _factory;

    public ProfilePictureEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string> ValidImages => new()
    {
        { "png", "image/png" },
        { "jpeg", "image/jpeg" },
        { "webp", "image/webp" },
    };

    [Theory]
    [MemberData(nameof(ValidImages))]
    public async Task PutPicture_WithValidImage_Returns200ProfileWithPictureVersion1(string sample, string contentType)
    {
        // Arrange
        var subject = await NewPlayerAsync();
        var bytes = Sample(sample);

        // Act
        using var response = await PutPictureAsync(subject, Image(bytes, contentType));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        AssertPrivateHeaders(response);
        var profile = await Json(response);
        profile.GetProperty("subject").GetString().Should().Be(subject);
        profile.GetProperty("displayName").GetString().Should().Be("Alex");
        var picture = profile.GetProperty("picture");
        picture.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("contentType", "updatedAt", "version");
        picture.GetProperty("contentType").GetString().Should().Be(contentType);
        picture.GetProperty("version").GetInt32().Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(ValidImages))]
    public async Task GetPicture_AfterUpload_ReturnsExactBytesContentTypeAndPrivateHeaders(string sample, string contentType)
    {
        // Arrange
        var subject = await NewPlayerAsync();
        var bytes = Sample(sample);
        (await PutPictureAsync(subject, Image(bytes, contentType))).StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        using var response = await SendAsync(HttpMethod.Get, PicturePath, Token(subject));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.ToString().Should().Be(contentType);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
        AssertPrivateHeaders(response);
    }

    [Fact]
    public async Task PutPicture_Replace_IncrementsVersionAndGetReturnsNewBytes()
    {
        // Arrange
        var subject = await NewPlayerAsync();
        (await PutPictureAsync(subject, Image(PictureSamples.Png, "image/png"))).StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        using var replaced = await PutPictureAsync(subject, Image(PictureSamples.WebpLossy, "image/webp"));
        using var read = await SendAsync(HttpMethod.Get, PicturePath, Token(subject));

        // Assert
        replaced.StatusCode.Should().Be(HttpStatusCode.OK);
        var picture = (await Json(replaced)).GetProperty("picture");
        picture.GetProperty("version").GetInt32().Should().Be(2);
        picture.GetProperty("contentType").GetString().Should().Be("image/webp");
        read.Content.Headers.ContentType?.MediaType.Should().Be("image/webp");
        (await read.Content.ReadAsByteArrayAsync()).Should().Equal(PictureSamples.WebpLossy);
    }

    [Fact]
    public async Task PutPicture_WithContentTypeCaseAndParameters_IsAcceptedAndStoredCanonically()
    {
        // Arrange
        var subject = await NewPlayerAsync();

        // Act
        using var response = await PutPictureAsync(subject, Image(PictureSamples.Png, "IMAGE/PNG; charset=x"));
        using var read = await SendAsync(HttpMethod.Get, PicturePath, Token(subject));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(response)).GetProperty("picture").GetProperty("contentType").GetString().Should().Be("image/png");
        read.Content.Headers.ContentType?.ToString().Should().Be("image/png");
    }

    [Fact]
    public async Task GetMe_AfterUpload_IncludesPictureInfo()
    {
        // Arrange
        var subject = await NewPlayerAsync();
        using var upload = await PutPictureAsync(subject, Image(PictureSamples.Jpeg, "image/jpeg"));
        var uploaded = (await Json(upload)).GetProperty("picture");

        // Act
        using var me = await SendAsync(HttpMethod.Get, "/api/users/me", Token(subject));

        // Assert
        var picture = (await Json(me)).GetProperty("picture");
        picture.GetProperty("contentType").GetString().Should().Be("image/jpeg");
        picture.GetProperty("version").GetInt32().Should().Be(1);
        picture.GetProperty("updatedAt").GetDateTime().Should().Be(uploaded.GetProperty("updatedAt").GetDateTime());
    }

    [Fact]
    public async Task GetMe_WithoutPicture_ReturnsPictureNull()
    {
        // Arrange
        var subject = await NewPlayerAsync();

        // Act
        using var me = await SendAsync(HttpMethod.Get, "/api/users/me", Token(subject));

        // Assert
        (await Json(me)).GetProperty("picture").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetPicture_WithoutProfileOrWithoutPicture_Returns404ContractProblem(bool withProfile)
    {
        // Arrange
        var subject = withProfile ? await NewPlayerAsync() : NewSubject();

        // Act
        using var response = await SendAsync(HttpMethod.Get, PicturePath, Token(subject));

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPicture_WithRangeHeader_ReturnsWholeImageWithoutRangeOrCacheValidators()
    {
        // Arrange
        var subject = await NewPlayerAsync();
        (await PutPictureAsync(subject, Image(PictureSamples.Png, "image/png"))).StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        using var response = await SendAsync(HttpMethod.Get, PicturePath, Token(subject), configure: request =>
            request.Headers.Range = new RangeHeaderValue(0, 3));

        // Assert: no partial content, no Accept-Ranges, no ETag/Last-Modified validators.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(PictureSamples.Png);
        response.Headers.AcceptRanges.Should().BeEmpty();
        response.Headers.ETag.Should().BeNull();
        response.Content.Headers.ContentRange.Should().BeNull();
        response.Content.Headers.LastModified.Should().BeNull();
    }

    [Fact]
    public async Task HeadPicture_IsNotSupportedAndReturnsNoImageBytes()
    {
        // Arrange
        var subject = await NewPlayerAsync();
        (await PutPictureAsync(subject, Image(PictureSamples.Png, "image/png"))).StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        using var response = await SendAsync(HttpMethod.Head, PicturePath, Token(subject));

        // Assert: the GET-only route does not match HEAD (405). TestServer keeps the status-code-pages problem body that
        // Kestrel would strip for HEAD; either way it is never the image.
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        var body = await response.Content.ReadAsByteArrayAsync();
        ContainsSequence(body, PictureSamples.Png[..8]).Should().BeFalse();
        response.Content.Headers.ContentType?.MediaType.Should().NotStartWith("image/");
    }

    [Fact]
    public async Task PutPicture_WithoutProfile_Returns404AndCreatesNothing()
    {
        // Arrange
        var subject = NewSubject();

        // Act
        using var response = await PutPictureAsync(subject, Image(PictureSamples.Png, "image/png"));

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.NotFound);
        await using var db = _factory.NewDbContext();
        (await db.UserProfiles.AnyAsync(p => p.Subject == subject)).Should().BeFalse();
    }

    [Fact]
    public async Task PutPicture_AtExactSizeLimit_Returns200()
    {
        // Arrange
        var subject = await NewPlayerAsync();
        var bytes = PictureSamples.PaddedTo(PictureSamples.Png, Limit);

        // Act
        using var response = await PutPictureAsync(subject, Image(bytes, "image/png"));
        using var read = await SendAsync(HttpMethod.Get, PicturePath, Token(subject));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadAsByteArrayAsync()).Should().HaveCount(Limit).And.Equal(bytes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PutPicture_OneByteOverLimit_Returns413(bool withContentLength)
    {
        // Arrange
        var subject = await NewPlayerAsync();
        var bytes = PictureSamples.PaddedTo(PictureSamples.Png, Limit + 1);
        HttpContent content = withContentLength
            ? new ByteArrayContent(bytes)
            : new StreamContent(new GeneratedStream(Limit + 1, bytes));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        // Act: the stream variant is sent chunked (the test client would otherwise still add a Content-Length).
        using var response = await SendAsync(HttpMethod.Put, PicturePath, Token(subject), content,
            request => request.Headers.TransferEncodingChunked = !withContentLength);

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.RequestEntityTooLarge);
        await ShouldHaveNoPictureAsync(subject);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PutPicture_WithHugeBody_Returns413WithoutReadingMoreThanTheLimit(bool declareContentLength)
    {
        // Arrange: a 50 MB generated body; the server-side counter sees every byte the API reads from Request.Body.
        const long huge = 50L * 1024 * 1024;
        var subject = await NewPlayerAsync();
        var counter = new BodyReadCounter();
        await using var counted = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter>(new CountingBodyStartupFilter(counter))));
        var content = new StreamContent(new GeneratedStream(huge, PictureSamples.Png));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        if (declareContentLength)
        {
            content.Headers.ContentLength = huge;
        }

        // Without an explicit chunked flag the test client still sends a Content-Length, so the cap on chunked reads
        // would never be exercised; the counter records what the server actually saw.
        void Chunked(HttpRequestMessage request) => request.Headers.TransferEncodingChunked = !declareContentLength;

        // Act
        using var response = await SendAsync(counted, HttpMethod.Put, PicturePath, Token(subject), content, Chunked);

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.RequestEntityTooLarge);
        counter.ContentLength.Should().Be(declareContentLength ? huge : null);
        counter.BytesRead.Should().BeLessThanOrEqualTo(declareContentLength ? 0 : Limit + 1);
        await ShouldHaveNoPictureAsync(subject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("image/jpg")]
    [InlineData("image/svg+xml")]
    [InlineData("image/gif")]
    [InlineData("application/octet-stream")]
    [InlineData("multipart/form-data; boundary=x")]
    [InlineData("text/plain")]
    [InlineData("application/json")]
    public async Task PutPicture_WithUnsupportedMediaType_Returns415ContractProblem(string? mediaType)
    {
        // Arrange
        var subject = await NewPlayerAsync();
        var content = new ByteArrayContent(PictureSamples.Png);
        if (mediaType is not null)
        {
            content.Headers.TryAddWithoutValidation("Content-Type", mediaType);
        }

        // Act
        using var response = await PutPictureAsync(subject, content);

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.UnsupportedMediaType);
        await ShouldHaveNoPictureAsync(subject);
    }

    [Fact]
    public async Task PutPicture_WithRealMultipartUpload_Returns415()
    {
        // Arrange
        var subject = await NewPlayerAsync();
        using var multipart = new MultipartFormDataContent { { Image(PictureSamples.Png, "image/png"), "file", "a.png" } };

        // Act
        using var response = await PutPictureAsync(subject, multipart);

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.UnsupportedMediaType);
        await ShouldHaveNoPictureAsync(subject);
    }

    [Theory]
    [InlineData("png-as-jpeg")]
    [InlineData("svg-as-png")]
    [InlineData("gif-as-webp")]
    [InlineData("jpeg-as-webp")]
    public async Task PutPicture_WithMisleadingContentType_Returns400NotUnsupportedMediaType(string variant)
    {
        // Arrange: the declared type is allowed but the bytes are something else (contract: 400, not 415).
        var subject = await NewPlayerAsync();
        var content = variant switch
        {
            "png-as-jpeg" => Image(PictureSamples.Png, "image/jpeg"),
            "svg-as-png" => Image(PictureSamples.Svg, "image/png"),
            "gif-as-webp" => Image(PictureSamples.Gif, "image/webp"),
            _ => Image(PictureSamples.Jpeg, "image/webp"),
        };

        // Act
        using var response = await PutPictureAsync(subject, content);

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.BadRequest);
        await ShouldHaveNoPictureAsync(subject);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("truncated-png")]
    [InlineData("truncated-jpeg")]
    [InlineData("truncated-webp")]
    [InlineData("webp-chunk-past-riff-end")]
    public async Task PutPicture_WithEmptyOrMalformedImage_Returns400(string variant)
    {
        // Arrange
        var subject = await NewPlayerAsync();
        var (bytes, type) = Malformed(variant);

        // Act
        using var response = await PutPictureAsync(subject, Image(bytes, type));

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.BadRequest);
        await ShouldHaveNoPictureAsync(subject);
    }

    [Fact]
    public async Task PutPicture_UnsupportedTypeAndOversized_Returns415()
    {
        // Arrange
        var subject = await NewPlayerAsync();

        // Act
        using var response = await PutPictureAsync(subject, Image(PictureSamples.PaddedTo(PictureSamples.Svg, Limit + 1), "image/svg+xml"));

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task PutPicture_OversizedAndMalformed_Returns413()
    {
        // Arrange
        var subject = await NewPlayerAsync();

        // Act
        using var response = await PutPictureAsync(subject, Image(PictureSamples.PaddedTo(PictureSamples.Svg, Limit + 1), "image/png"));

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.RequestEntityTooLarge);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("truncated-png")]
    public async Task PutPicture_MalformedWithoutProfile_Returns400BeforeNotFound(string variant)
    {
        // Arrange
        var (bytes, type) = Malformed(variant);

        // Act
        using var response = await PutPictureAsync(NewSubject(), Image(bytes, type));

        // Assert
        await AssertContractProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("malformed", HttpStatusCode.BadRequest)]
    [InlineData("mismatch", HttpStatusCode.BadRequest)]
    [InlineData("empty", HttpStatusCode.BadRequest)]
    [InlineData("webp-chunk-past-riff-end", HttpStatusCode.BadRequest)]
    [InlineData("oversized", HttpStatusCode.RequestEntityTooLarge)]
    [InlineData("unsupported", HttpStatusCode.UnsupportedMediaType)]
    [InlineData("no-token", HttpStatusCode.Unauthorized)]
    [InlineData("read-only-token", HttpStatusCode.Forbidden)]
    public async Task PutPicture_EachRejection_PreservesProfileAndPicture(string rejection, HttpStatusCode expected)
    {
        // Arrange
        var subject = await NewPlayerAsync();
        (await PutPictureAsync(subject, Image(PictureSamples.Png, "image/png"))).StatusCode.Should().Be(HttpStatusCode.OK);
        var token = rejection switch
        {
            "no-token" => null,
            "read-only-token" => Token(subject, "profiles.read"),
            _ => Token(subject),
        };
        var content = rejection switch
        {
            "malformed" => Image(PictureSamples.Jpeg[..10], "image/jpeg"),
            "mismatch" => Image(PictureSamples.Png, "image/webp"),
            "empty" => Image([], "image/png"),
            "webp-chunk-past-riff-end" => Image(Malformed("webp-chunk-past-riff-end").Bytes, "image/webp"),
            "oversized" => Image(PictureSamples.PaddedTo(PictureSamples.WebpLossy, Limit + 1), "image/webp"),
            "unsupported" => Image(PictureSamples.Gif, "image/gif"),
            _ => Image(PictureSamples.WebpLossy, "image/webp"),
        };

        // Act
        using var response = await SendAsync(HttpMethod.Put, PicturePath, token, content);

        // Assert
        response.StatusCode.Should().Be(expected);
        AssertPrivateHeaders(response);
        await ShouldStillHaveOriginalPngAsync(subject);
    }

    [Fact]
    public async Task PictureRoutes_DifferentAccount_CannotReadOrReplaceOthersPicture()
    {
        // Arrange
        var alice = await NewPlayerAsync();
        (await PutPictureAsync(alice, Image(PictureSamples.Png, "image/png"))).StatusCode.Should().Be(HttpStatusCode.OK);
        var bob = NewSubject();

        // Act
        using var bobReads = await SendAsync(HttpMethod.Get, PicturePath, Token(bob));
        using var bobWrites = await PutPictureAsync(bob, Image(PictureSamples.Jpeg, "image/jpeg"));

        // Assert
        bobReads.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await bobReads.Content.ReadAsByteArrayAsync()).Should().NotEqual(PictureSamples.Png);
        bobWrites.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ShouldStillHaveOriginalPngAsync(alice);
    }

    [Fact]
    public async Task PictureRoutes_WithSpoofedOwnerReferences_UseOnlyTokenIdentity()
    {
        // Arrange
        var alice = await NewPlayerAsync();
        using var aliceUpload = await PutPictureAsync(alice, Image(PictureSamples.Png, "image/png"));
        var aliceId = (await Json(aliceUpload)).GetProperty("id").GetGuid();
        var bob = await NewPlayerAsync();
        var spoofedPath = $"{PicturePath}?userId={aliceId}&profileId={aliceId}&subject={alice}";
        void Spoof(HttpRequestMessage request)
        {
            request.Headers.Add("X-User-Id", aliceId.ToString());
            request.Headers.Add("X-Profile-Id", aliceId.ToString());
        }

        // Act
        using var bobReads = await SendAsync(HttpMethod.Get, spoofedPath, Token(bob), configure: Spoof);
        using var bobWrites = await SendAsync(HttpMethod.Put, spoofedPath, Token(bob), Image(PictureSamples.Jpeg, "image/jpeg"), Spoof);
        using var byIdRead = await SendAsync(HttpMethod.Get, $"/api/users/{aliceId}/picture", Token(bob));
        using var byIdWrite = await SendAsync(HttpMethod.Put, $"/api/users/{aliceId}/picture", Token(bob), Image(PictureSamples.Jpeg, "image/jpeg"));

        // Assert
        bobReads.StatusCode.Should().Be(HttpStatusCode.NotFound);
        bobWrites.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(bobWrites)).GetProperty("subject").GetString().Should().Be(bob);
        byIdRead.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        byIdWrite.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await byIdRead.Content.ReadAsByteArrayAsync()).Should().NotEqual(PictureSamples.Png);
        await ShouldStillHaveOriginalPngAsync(alice);
    }

    [Fact]
    public async Task OpenApiDocument_PutPicture_DocumentsRequiredRawImageBody()
    {
        // Act
        using var response = await SendAsync(HttpMethod.Get, "/openapi/v1.json", token: null);
        var operation = (await Json(response)).GetProperty("paths").GetProperty(PicturePath).GetProperty("put");

        // Assert
        var body = operation.GetProperty("requestBody");
        body.GetProperty("required").GetBoolean().Should().BeTrue();
        var content = body.GetProperty("content");
        content.EnumerateObject().Select(media => media.Name).Should().BeEquivalentTo("image/png", "image/jpeg", "image/webp");
        content.EnumerateObject().Should().OnlyContain(media =>
            media.Value.GetProperty("schema").GetProperty("type").GetString() == "string"
            && media.Value.GetProperty("schema").GetProperty("format").GetString() == "binary");
    }

    private static string NewSubject() => $"player-{Guid.NewGuid():N}";

    private static string Token(string subject, string scope = "profiles.read profiles.write") =>
        TestTokens.Create(ActorKind.User, scope, claims => claims["sub"] = subject);

    private static byte[] Sample(string name) => name switch
    {
        "png" => PictureSamples.Png,
        "jpeg" => PictureSamples.Jpeg,
        _ => PictureSamples.WebpLossless,
    };

    private static (byte[] Bytes, string Type) Malformed(string variant) => variant switch
    {
        "empty" => ([], "image/png"),
        "truncated-png" => (PictureSamples.Png[..20], "image/png"),
        "truncated-jpeg" => (PictureSamples.Jpeg[..22], "image/jpeg"),
        "truncated-webp" => (PictureSamples.WebpLossless[..24], "image/webp"),
        // A VP8L chunk whose declared size runs past the RIFF end into trailing body bytes.
        "webp-chunk-past-riff-end" => (WebpChunkPastRiffEnd(), "image/webp"),
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    private static byte[] WebpChunkPastRiffEnd()
    {
        var bytes = PictureSamples.PaddedTo(PictureSamples.WebpLossless, PictureSamples.WebpLossless.Length + 20);
        bytes[16] = 23;
        return bytes;
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    private static ByteArrayContent Image(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        return content;
    }

    private static void AssertPrivateHeaders(HttpResponseMessage response)
    {
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.NoStore.Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    // Contract Problem schema: exactly type, title, status, detail and traceId, all non-empty; plus private headers.
    private static async Task AssertContractProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        AssertPrivateHeaders(response);
        var problem = await Json(response);
        problem.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("type", "title", "status", "detail", "traceId");
        problem.GetProperty("status").GetInt32().Should().Be((int)status);
        foreach (var field in new[] { "type", "title", "detail", "traceId" })
        {
            problem.GetProperty(field).GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    private async Task<string> NewPlayerAsync()
    {
        var subject = NewSubject();
        using var created = await SendAsync(HttpMethod.Put, "/api/users/me", Token(subject),
            new StringContent("""{ "displayName": "Alex" }""", Encoding.UTF8, "application/json"));
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        return subject;
    }

    private Task<HttpResponseMessage> PutPictureAsync(string subject, HttpContent content) =>
        SendAsync(HttpMethod.Put, PicturePath, Token(subject), content);

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string? token, HttpContent? content = null, Action<HttpRequestMessage>? configure = null) =>
        SendAsync(_factory, method, path, token, content, configure);

    private static async Task<HttpResponseMessage> SendAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<QuizArena.Identity.Api.Program> factory,
        HttpMethod method,
        string path,
        string? token,
        HttpContent? content = null,
        Action<HttpRequestMessage>? configure = null)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        configure?.Invoke(request);
        var response = await client.SendAsync(request);
        await response.Content.LoadIntoBufferAsync();
        return response;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task ShouldHaveNoPictureAsync(string subject)
    {
        await using var db = _factory.NewDbContext();
        var profile = await db.UserProfiles.SingleAsync(p => p.Subject == subject);
        profile.DisplayName.Should().Be("Alex");
        (await db.ProfilePictures.AnyAsync(picture => picture.ProfileId == profile.Id)).Should().BeFalse();
    }

    private async Task ShouldStillHaveOriginalPngAsync(string subject)
    {
        using var me = await SendAsync(HttpMethod.Get, "/api/users/me", Token(subject));
        using var read = await SendAsync(HttpMethod.Get, PicturePath, Token(subject));
        var profile = await Json(me);
        profile.GetProperty("displayName").GetString().Should().Be("Alex");
        profile.GetProperty("picture").GetProperty("version").GetInt32().Should().Be(1);
        profile.GetProperty("picture").GetProperty("contentType").GetString().Should().Be("image/png");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadAsByteArrayAsync()).Should().Equal(PictureSamples.Png);
    }

    /// <summary>A non-seekable body of <c>length</c> bytes (prefix, then zeros) generated on the fly: sent chunked.</summary>
    private sealed class GeneratedStream(long length, byte[] prefix) : Stream
    {
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var toWrite = (int)Math.Min(count, length - _position);
            for (var i = 0; i < toWrite; i++)
            {
                var index = _position + i;
                buffer[offset + i] = index < prefix.Length ? prefix[index] : (byte)0;
            }

            _position += toWrite;
            return toWrite;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class BodyReadCounter
    {
        private long _bytesRead;

        public long BytesRead => Interlocked.Read(ref _bytesRead);

        /// <summary>The Content-Length the server saw on the last request (null for a chunked body).</summary>
        public long? ContentLength { get; set; }

        public void Add(int count) => Interlocked.Add(ref _bytesRead, count);
    }

    /// <summary>Wraps every request body first in the pipeline so the test sees exactly what the API reads.</summary>
    private sealed class CountingBodyStartupFilter(BodyReadCounter counter) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                counter.ContentLength = context.Request.ContentLength;
                context.Request.Body = new CountingStream(context.Request.Body, counter);
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    private sealed class CountingStream(Stream inner, BodyReadCounter counter) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            counter.Add(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            counter.Add(read);
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
