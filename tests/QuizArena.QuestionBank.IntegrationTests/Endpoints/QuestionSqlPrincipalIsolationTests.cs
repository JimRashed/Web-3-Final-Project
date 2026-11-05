using System.Net;
using FluentAssertions;
using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.IntegrationTests.Security;
using Xunit;

namespace QuizArena.QuestionBank.IntegrationTests.Endpoints;

public sealed class QuestionSqlPrincipalIsolationTests : IAsyncLifetime
{
    private readonly QuestionBankSqlApiFixture _fixture = new();
    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Route_BearerWithoutExpiry_ReturnsUnauthorizedWithoutDisclosingOrChangingCatalog(bool machine)
    {
        // Arrange
        var before = await _fixture.ReadCatalogAsync();
        var token = TestTokens.Create(machine ? ActorKind.Machine : ActorKind.User,
            machine ? QuestionBankPolicies.QuestionsPlay : QuestionBankPolicies.QuestionsRead, includeExpiry: false);

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(_fixture.Client, machine, token);

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle().Which.Scheme.Should().Be("Bearer");
        (await _fixture.ReadCatalogAsync()).Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    public static IEnumerable<object[]> ForbiddenPrincipals()
    {
        foreach (var machine in new[] { false, true })
            foreach (var invalid in new[] { "empty-subject", "whitespace-subject", "opposite-principal", "wrong-client", "missing-client", "case-actor", "case-client" })
                yield return [machine, invalid];
    }

    [Theory]
    [MemberData(nameof(ForbiddenPrincipals))]
    public async Task Route_IneligibleSignedPrincipal_ReturnsForbiddenWithoutDisclosingOrChangingCatalog(bool machine, string invalid)
    {
        // Arrange
        var before = await _fixture.ReadCatalogAsync();
        var token = TestTokens.Create(machine ? ActorKind.Machine : ActorKind.User,
            machine ? QuestionBankPolicies.QuestionsPlay : QuestionBankPolicies.QuestionsRead, claims =>
            {
                switch (invalid)
                {
                    case "empty-subject": claims["sub"] = ""; break;
                    case "whitespace-subject": claims["sub"] = " \t "; break;
                    case "opposite-principal":
                        claims["qa_actor"] = machine ? "user" : "machine";
                        claims["azp"] = machine ? TestTokens.BrowserClient : TestTokens.MachineClient;
                        break;
                    case "wrong-client": claims["azp"] = "unrelated-client"; break;
                    case "missing-client": claims.Remove("azp"); break;
                    case "case-actor": claims["qa_actor"] = machine ? "Machine" : "User"; break;
                    case "case-client": claims["azp"] = (machine ? TestTokens.MachineClient : TestTokens.BrowserClient).ToUpperInvariant(); break;
                }
            });

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(_fixture.Client, machine, token);

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, HttpStatusCode.Forbidden);
        (await _fixture.ReadCatalogAsync()).Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }
}
