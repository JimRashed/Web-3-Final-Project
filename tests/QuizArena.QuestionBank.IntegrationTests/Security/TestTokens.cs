using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QuizArena.QuestionBank.Api.Security;

namespace QuizArena.QuestionBank.IntegrationTests.Security;

/// <summary>Signs tokens with ephemeral RSA keys so the real JwtBearer handler can validate them.</summary>
internal static class TestTokens
{
    public const string Issuer = "http://localhost:8080/realms/quizarena";
    public const string Audience = "quizarena-questionbank";
    public const string BrowserClient = "quizarena-client";
    public const string MachineClient = "quizarena-gameplay";
    public const string UserSubject = "player-a";
    public const string MachineSubject = "service-account-quizarena-gameplay";

    public static RsaSecurityKey SigningKey { get; } = CreateKey("test-signing-key");

    /// <summary>Same key ID as <see cref="SigningKey"/> but different key material: produces a bad signature.</summary>
    public static RsaSecurityKey ImpostorKey { get; } = CreateKey("test-signing-key");

    public static RsaSecurityKey UnknownKey { get; } = CreateKey("unknown-key");

    public static string Create(
        ActorKind actor,
        string scope,
        Action<Dictionary<string, object>>? customize = null,
        SecurityKey? key = null,
        DateTime? expires = null,
        bool includeExpiry = true)
    {
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = Audience,
            ["sub"] = actor == ActorKind.User ? UserSubject : MachineSubject,
            ["typ"] = "Bearer",
            ["azp"] = actor == ActorKind.User ? BrowserClient : MachineClient,
            ["qa_actor"] = actor == ActorKind.User ? "user" : "machine",
            ["scope"] = scope,
        };
        customize?.Invoke(claims);

        var expiry = expires ?? DateTime.UtcNow.AddMinutes(5);
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            IssuedAt = expiry.AddMinutes(-10),
            NotBefore = expiry.AddMinutes(-10),
            Expires = includeExpiry ? expiry : null,
            SigningCredentials = new SigningCredentials(key ?? SigningKey, SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    private static RsaSecurityKey CreateKey(string keyId) => new(RSA.Create(2048)) { KeyId = keyId };
}
