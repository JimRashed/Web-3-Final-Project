using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Endpoints;

/// <summary>
/// The full bearer rejection and grant matrix against the real /api/users/me and /api/users/me/picture routes (the
/// security matrix tests only cover a probe controller), plus impersonation attempts. Rejected writes persist nothing.
/// </summary>
public sealed class ProfileRouteSecurityTests : IClassFixture<IdentityApiFactory>
{
    private const string Scopes = "profiles.read profiles.write";
    private readonly IdentityApiFactory _factory;

    public ProfileRouteSecurityTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] Routes = ["GET /me", "PUT /me", "GET /me/picture", "PUT /me/picture"];

    public static TheoryData<string, string> UnauthenticatedCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var route in Routes)
        {
            foreach (var kind in new[] { "missing", "malformed", "bad-signature", "unknown-key", "expired", "no-expiry", "wrong-issuer", "wrong-audience", "id-token-type" })
            {
                data.Add(route, kind);
            }
        }

        return data;
    }

    public static TheoryData<string, string> ForbiddenCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var route in Routes)
        {
            foreach (var kind in new[] { "no-scope", "scope-prefix", "scope-suffix", "no-subject", "blank-subject", "other-client", "no-actor-claim", "machine-actor-claim", "machine-token" })
            {
                data.Add(route, kind);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(UnauthenticatedCases))]
    public async Task MeRoutes_WithMissingOrInvalidToken_Return401ChallengeAndWriteNothing(string route, string kind)
    {
        // Arrange
        var subject = NewSubject();
        await ArrangeOwnerAsync(route, subject);
        var token = kind switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "bad-signature" => UserToken(subject, key: TestTokens.ImpostorKey),
            "unknown-key" => UserToken(subject, key: TestTokens.UnknownKey),
            "expired" => TestTokens.Create(ActorKind.User, Scopes, claims => claims["sub"] = subject, expires: DateTime.UtcNow.AddMinutes(-5)),
            "no-expiry" => TestTokens.Create(ActorKind.User, Scopes, claims => claims["sub"] = subject, includeExpiry: false),
            "wrong-issuer" => UserToken(subject, claims => claims["iss"] = "http://localhost:8080/realms/other"),
            "wrong-audience" => UserToken(subject, claims => claims["aud"] = "quizarena-statistics"),
            "id-token-type" => UserToken(subject, claims => claims["typ"] = "ID"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // Act
        using var response = await SendRoute(route, token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var challenge = response.Headers.WwwAuthenticate.Should().ContainSingle(header => header.Scheme == "Bearer").Subject;
        challenge.Parameter.Should().Be(kind == "missing" ? null : "error=\"invalid_token\"");
        await ShouldHaveWrittenNothing(route, subject);
    }

    [Theory]
    [MemberData(nameof(ForbiddenCases))]
    public async Task MeRoutes_WithValidTokenLackingGrantOrIdentity_Return403AndWriteNothing(string route, string kind)
    {
        // Arrange
        var subject = NewSubject();
        await ArrangeOwnerAsync(route, subject);
        var token = kind switch
        {
            "no-scope" => TestTokens.Create(ActorKind.User, "openid profile", claims => claims["sub"] = subject),
            "scope-prefix" => TestTokens.Create(ActorKind.User, "profiles.readx profiles.writex", claims => claims["sub"] = subject),
            "scope-suffix" => TestTokens.Create(ActorKind.User, "xprofiles.read xprofiles.write", claims => claims["sub"] = subject),
            "no-subject" => UserToken(subject, claims => claims.Remove("sub")),
            "blank-subject" => UserToken(subject, claims => claims["sub"] = "   "),
            "other-client" => UserToken(subject, claims => claims["azp"] = TestTokens.MachineClient),
            "no-actor-claim" => UserToken(subject, claims => claims.Remove("qa_actor")),
            "machine-actor-claim" => UserToken(subject, claims => claims["qa_actor"] = "machine"),
            "machine-token" => TestTokens.Create(ActorKind.Machine, Scopes, claims => claims["sub"] = subject),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // Act
        using var response = await SendRoute(route, token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        await ShouldHaveWrittenNothing(route, subject);
    }

    [Fact]
    public async Task GetMe_WithCaseVariantSubject_DoesNotReturnAnotherPlayersProfile()
    {
        // Arrange
        var subject = $"Player-{Guid.NewGuid():N}";
        using var created = await SendMe("PUT", UserToken(subject), """{ "displayName": "Owner" }""");

        // Act
        using var variant = await SendMe("GET", UserToken(subject.ToLowerInvariant()));
        using var owner = await SendMe("GET", UserToken(subject));

        // Assert
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        variant.StatusCode.Should().Be(HttpStatusCode.NotFound);
        owner.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutMe_WithSameSubjectFromAnotherTrustedIssuerPrefix_IsRejectedBeforeWriting()
    {
        // Arrange: an issuer that only shares a prefix with the configured realm must not authenticate.
        var subject = NewSubject();
        var token = UserToken(subject, claims => claims["iss"] = $"{TestTokens.Issuer}-evil");

        // Act
        using var response = await SendMe("PUT", token, """{ "displayName": "Mallory" }""");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await ShouldHaveNoProfile(subject);
    }

    [Theory]
    [InlineData("GET /me/picture", "profiles.write")]
    [InlineData("PUT /me/picture", "profiles.read")]
    public async Task PictureRoutes_WithOnlyTheOtherScope_Return403AndWriteNothing(string route, string scope)
    {
        // Arrange
        var subject = NewSubject();
        await ArrangeOwnerAsync(route, subject);
        var token = TestTokens.Create(ActorKind.User, scope, claims => claims["sub"] = subject);

        // Act
        using var response = await SendRoute(route, token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ShouldHaveWrittenNothing(route, subject);
    }

    [Theory]
    [InlineData("read-only", "svg", HttpStatusCode.Forbidden)]
    [InlineData("read-only", "oversized", HttpStatusCode.Forbidden)]
    [InlineData("none", "oversized", HttpStatusCode.Unauthorized)]
    [InlineData("none", "svg", HttpStatusCode.Unauthorized)]
    public async Task PutPicture_WithoutGrant_IsRejectedBeforeMediaTypeAndSizeChecks(string grant, string body, HttpStatusCode expected)
    {
        // Arrange: authorization (401/403) is decided before 415 and 413 (DR-9).
        var subject = NewSubject();
        await ArrangeOwnerAsync("PUT /me/picture", subject);
        var token = grant == "none" ? null : TestTokens.Create(ActorKind.User, "profiles.read", claims => claims["sub"] = subject);
        var content = body == "svg"
            ? Image(PictureSamples.Svg, "image/svg+xml")
            : Image(PictureSamples.PaddedTo(PictureSamples.Png, 3 * 1024 * 1024), "image/png");

        // Act
        using var response = await SendRoute("PUT /me/picture", token, content);

        // Assert
        response.StatusCode.Should().Be(expected);
        await ShouldHaveWrittenNothing("PUT /me/picture", subject);
    }

    private static string NewSubject() => $"player-{Guid.NewGuid():N}";

    private static string UserToken(string subject, Action<Dictionary<string, object>>? customize = null, Microsoft.IdentityModel.Tokens.SecurityKey? key = null) =>
        TestTokens.Create(ActorKind.User, Scopes, claims =>
        {
            claims["sub"] = subject;
            customize?.Invoke(claims);
        }, key);

    private static ByteArrayContent Image(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return content;
    }

    private Task<HttpResponseMessage> SendMe(string method, string? token, string body = """{ "displayName": "Intruder" }""") =>
        SendRoute($"{method} /me", token, new StringContent(body, Encoding.UTF8, "application/json"));

    // Route keys are "<METHOD> /me" or "<METHOD> /me/picture"; PUT sends a JSON name or a valid PNG unless given content.
    private async Task<HttpResponseMessage> SendRoute(string route, string? token, HttpContent? content = null)
    {
        var (method, path) = (route[..route.IndexOf(' ', StringComparison.Ordinal)], route[(route.IndexOf(' ', StringComparison.Ordinal) + 1)..]);
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/users" + path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (method == "PUT")
        {
            request.Content = content ?? (path == "/me/picture"
                ? Image(PictureSamples.Png, "image/png")
                : new StringContent("""{ "displayName": "Intruder" }""", Encoding.UTF8, "application/json"));
        }

        return await client.SendAsync(request);
    }

    // Picture routes need an existing owner profile so "nothing written" can mean "no picture row" (and name kept).
    private async Task ArrangeOwnerAsync(string route, string subject)
    {
        if (!route.EndsWith("/picture", StringComparison.Ordinal))
        {
            return;
        }

        using var created = await SendMe("PUT", UserToken(subject), """{ "displayName": "Owner" }""");
        created.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task ShouldHaveWrittenNothing(string route, string subject)
    {
        if (!route.EndsWith("/picture", StringComparison.Ordinal))
        {
            await ShouldHaveNoProfile(subject);
            return;
        }

        await using var db = _factory.NewDbContext();
        var profile = await db.UserProfiles.SingleAsync(p => p.Subject == subject);
        profile.DisplayName.Should().Be("Owner");
        (await db.ProfilePictures.AnyAsync(picture => picture.ProfileId == profile.Id)).Should().BeFalse();
    }

    private async Task ShouldHaveNoProfile(string subject)
    {
        await using var db = _factory.NewDbContext();
        (await db.UserProfiles.AnyAsync(profile => profile.Subject == subject)).Should().BeFalse();
    }
}
