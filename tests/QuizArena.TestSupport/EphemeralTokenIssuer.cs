using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace QuizArena.TestSupport;

/// <summary>
/// Issues real, signed access tokens for integration tests and publishes the matching
/// discovery metadata, so the production <c>JwtBearer</c> pipeline validates them for real.
/// One instance per test suite; the RSA key is ephemeral.
/// </summary>
public sealed class EphemeralTokenIssuer : IDisposable
{
    public const string DefaultIssuer = "http://localhost:8080/realms/quizarena";
    public const string UserActor = "user";
    public const string MachineActor = "machine";
    public const string AccessTokenType = "Bearer";

    private readonly RSA _rsa;

    public EphemeralTokenIssuer(string issuer = DefaultIssuer)
    {
        Issuer = issuer;
        _rsa = RSA.Create(2048);
        SigningKey = new RsaSecurityKey(_rsa) { KeyId = "quizarena-test-key" };

        Metadata = new OpenIdConnectConfiguration { Issuer = issuer };
        Metadata.SigningKeys.Add(SigningKey);
    }

    public string Issuer { get; }

    public RsaSecurityKey SigningKey { get; }

    /// <summary>Discovery document handed to the host factory instead of a real Keycloak.</summary>
    public OpenIdConnectConfiguration Metadata { get; }

    /// <summary>
    /// A user token as the browser client would obtain it through authorization code + PKCE.
    /// </summary>
    public string UserToken(string audience, string subject, string scope, string clientId = "quizarena-client", DateTime? expires = null) =>
        Create(audience, UserActor, subject, clientId, scope, expires);

    /// <summary>
    /// A machine token as the Gameplay service would obtain it through client credentials.
    /// </summary>
    public string MachineToken(string audience, string subject, string scope, string clientId = "quizarena-gameplay", DateTime? expires = null) =>
        Create(audience, MachineActor, subject, clientId, scope, expires);

    /// <summary>
    /// Builds a token with explicit control over every claim that the API validates, so a test can
    /// exercise exactly one negative condition at a time.
    /// </summary>
    public string Create(
        string audience,
        string actor,
        string subject,
        string clientId,
        string scope,
        DateTime? expires = null,
        string? issuer = null,
        string tokenType = AccessTokenType,
        SecurityKey? signingKey = null,
        bool includeExpiry = true)
    {
        var claims = new Dictionary<string, object>
        {
            ["iss"] = issuer ?? Issuer,
            ["aud"] = audience,
            ["sub"] = subject,
            ["azp"] = clientId,
            ["qa_actor"] = actor,
            ["scope"] = scope,
            ["typ"] = tokenType,
        };

        var expiry = expires ?? DateTime.UtcNow.AddMinutes(5);
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            IssuedAt = expiry.AddMinutes(-10),
            NotBefore = expiry.AddMinutes(-10),
            Expires = includeExpiry ? expiry : null,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    /// <summary>
    /// A key with the same <c>kid</c> as <see cref="SigningKey"/> but different material: the
    /// handler resolves the key by id and the signature check must still fail.
    /// </summary>
    public RsaSecurityKey CreateImpostorKey()
    {
        var impostor = new RsaSecurityKey(RSA.Create(2048)) { KeyId = SigningKey.KeyId };
        return impostor;
    }

    public void Dispose() => _rsa.Dispose();
}
