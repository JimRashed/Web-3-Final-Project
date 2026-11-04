using System.Security.Claims;
using FluentAssertions;
using QuizArena.Statistics.Api.Security;
using Xunit;

namespace QuizArena.Statistics.IntegrationTests.Security;

/// <summary>Trusted subject resolution: the actor comes only from validated token claims, never from defaults.</summary>
public sealed class AuthenticatedActorTests
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value, ClaimValueTypes.String, Issuer)), "Bearer"));

    [Theory]
    [InlineData("user", ActorKind.User)]
    [InlineData("machine", ActorKind.Machine)]
    public void GetAuthenticatedActor_WithSubjectIssuerAndActor_ReturnsExactValues(string actor, ActorKind expected)
    {
        // Arrange
        var principal = Principal(("sub", "Player-A"), ("iss", Issuer), ("qa_actor", actor));

        // Act
        var result = principal.GetAuthenticatedActor();

        // Assert
        result.Should().Be(new AuthenticatedActor(Issuer, "Player-A", expected));
    }

    [Fact]
    public void GetAuthenticatedActor_WithoutIssClaim_UsesTheValidatedTokenIssuer()
    {
        // Arrange
        var principal = Principal(("sub", "player-a"), ("qa_actor", "user"));

        // Act
        var result = principal.GetAuthenticatedActor();

        // Assert
        result.Issuer.Should().Be(Issuer);
    }

    public static TheoryData<string> MissingClaimCases() => new() { "no-sub", "blank-sub", "no-actor", "unknown-actor", "actor-case" };

    [Theory]
    [MemberData(nameof(MissingClaimCases))]
    public void GetAuthenticatedActor_WithMissingOrUnknownClaims_Throws(string kind)
    {
        // Arrange
        var principal = kind switch
        {
            "no-sub" => Principal(("iss", Issuer), ("qa_actor", "user")),
            "blank-sub" => Principal(("sub", " "), ("iss", Issuer), ("qa_actor", "user")),
            "no-actor" => Principal(("sub", "player-a"), ("iss", Issuer)),
            "unknown-actor" => Principal(("sub", "player-a"), ("iss", Issuer), ("qa_actor", "admin")),
            "actor-case" => Principal(("sub", "player-a"), ("iss", Issuer), ("qa_actor", "User")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // Act
        var act = () => principal.GetAuthenticatedActor();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetAuthenticatedActor_WithAnonymousPrincipal_Throws()
    {
        // Act
        var act = () => new ClaimsPrincipal(new ClaimsIdentity()).GetAuthenticatedActor();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
