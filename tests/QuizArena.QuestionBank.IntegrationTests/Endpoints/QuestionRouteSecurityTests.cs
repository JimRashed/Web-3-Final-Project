using System.Net;
using FluentAssertions;
using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.IntegrationTests.Security;
using Xunit;

namespace QuizArena.QuestionBank.IntegrationTests.Endpoints;

public sealed class QuestionRouteSecurityTests
{
    public static IEnumerable<object[]> InvalidTokens()
    {
        foreach (var machine in new[] { false, true })
        {
            foreach (var invalid in new[] { "missing", "malformed", "signature", "key", "expired", "issuer", "audience", "id-token" })
                yield return [machine, invalid];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task Route_InvalidBearer_ReturnsUnauthorizedWithChallenge(bool machine, string invalid)
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();
        var kind = machine ? ActorKind.Machine : ActorKind.User;
        var scope = machine ? QuestionBankPolicies.QuestionsPlay : QuestionBankPolicies.QuestionsRead;
        var token = invalid switch
        {
            "missing" => null,
            "malformed" => "SENTINEL",
            "signature" => TestTokens.Create(kind, scope, key: TestTokens.ImpostorKey),
            "key" => TestTokens.Create(kind, scope, key: TestTokens.UnknownKey),
            "expired" => TestTokens.Create(kind, scope, expires: DateTime.UtcNow.AddMinutes(-5)),
            "issuer" => TestTokens.Create(kind, scope, claims => claims["iss"] = "https://wrong-issuer.invalid"),
            "audience" => TestTokens.Create(kind, scope, claims => claims["aud"] = "wrong-audience"),
            _ => TestTokens.Create(kind, scope, claims => claims["typ"] = "ID"),
        };

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, machine, token);

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle().Which.Scheme.Should().Be("Bearer");
    }

    public static IEnumerable<object[]> ForbiddenTokens()
    {
        foreach (var machine in new[] { false, true })
            foreach (var invalid in new[] { "missing-scope", "wrong-scope", "substring", "actor", "client", "missing-actor", "missing-subject" })
                yield return [machine, invalid];
    }

    [Theory]
    [MemberData(nameof(ForbiddenTokens))]
    public async Task Route_WrongScopeOrIdentity_ReturnsForbidden(bool machine, string invalid)
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();
        var kind = machine ? ActorKind.Machine : ActorKind.User;
        var scope = machine ? QuestionBankPolicies.QuestionsPlay : QuestionBankPolicies.QuestionsRead;
        var token = TestTokens.Create(kind, scope, claims =>
        {
            switch (invalid)
            {
                case "missing-scope": claims.Remove("scope"); break;
                case "wrong-scope": claims["scope"] = "unrelated"; break;
                case "substring": claims["scope"] = $"prefix.{scope}.suffix"; break;
                case "actor": claims["qa_actor"] = machine ? "user" : "machine"; break;
                case "client": claims["azp"] = machine ? TestTokens.BrowserClient : TestTokens.MachineClient; break;
                case "missing-actor": claims.Remove("qa_actor"); break;
                case "missing-subject": claims.Remove("sub"); break;
            }
        });

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, machine, token);

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Route_ExactScopeOrScp_ReturnsSuccess(bool machine, bool scp)
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();
        var scope = machine ? QuestionBankPolicies.QuestionsPlay : QuestionBankPolicies.QuestionsRead;
        var token = TestTokens.Create(machine ? ActorKind.Machine : ActorKind.User, $"other {scope} extra", claims =>
        {
            if (scp) { claims.Remove("scope"); claims["scp"] = new[] { "other", scope }; }
        });

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, machine, token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
