using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Lobby.Api.Contracts;
using QuizArena.Lobby.Api.Security;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Endpoints;

public sealed class LobbyInputTests(LobbyApiFixture fixture) : IClassFixture<LobbyApiFixture>
{
    public static IEnumerable<object[]> InvalidCodes() =>
        from route in new[] { "preview", "join", "lock", "verify" }
        from code in new[] { "abc123", "AbC123", "ABC12", "ABC1234", "ABC-12" }
        select new object[] { route, code };

    [Theory]
    [MemberData(nameof(InvalidCodes))]
    public async Task CodeRoute_InvalidSyntax_Returns400InsteadOfRoute404(string route, string code)
    {
        // Arrange
        var (method, path, scope, actor) = Route(route, code);

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, method, path, LobbyApiFixture.Token(scope, actor: actor), route == "join" ? "{}" : null);

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 400);
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("join")]
    [InlineData("lock")]
    [InlineData("verify")]
    public async Task CodeRoute_ValidMissingLobby_Returns404(string route)
    {
        // Arrange
        var deleted = await fixture.SeedAsync();
        await using var context = fixture.Database.CreateContext();
        await context.ActiveLobbies.Where(lobby => lobby.Id == deleted.Id).ExecuteDeleteAsync();
        var (method, path, scope, actor) = Route(route, deleted.JoinCode);

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, method, path, LobbyApiFixture.Token(scope, "outsider", actor), route == "join" ? "{}" : null);

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 404);
    }

    public static IEnumerable<object?[]> InvalidBodies() =>
        from route in new[] { "create", "join", "lock" }
        from body in new[] { "null", "{", "1", "true", "[]", "{\"hostId\":\"SECRET-SPOOF\"}", "{\"HostId\":\"forged\"}", "{\"playerId\":\"forged\"}", "{\"USERID\":\"forged\"}", "{\"extra\":1}" }
        select new object?[] { route, body };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Mutation_SuppliedInvalidBody_ReturnsSafe400WithoutWriting(string route, string body)
    {
        // Arrange
        var lobby = await fixture.SeedAsync(full: true);
        await using var before = fixture.Database.CreateContext();
        var count = await before.ActiveLobbies.CountAsync();
        var path = route == "create" ? "/api/lobbies" : $"/api/lobbies/{lobby.JoinCode}/{route}";

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, path, LobbyApiFixture.Token(), body);

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 400);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("SECRET-SPOOF").And.NotContain("forged");
        await using var reader = fixture.Database.CreateContext();
        (await reader.ActiveLobbies.CountAsync()).Should().Be(count);
        (await fixture.Database.Repository().GetByJoinCodeAsync(lobby.JoinCode))!.Status.ToString().Should().Be("Open");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("join")]
    public async Task Mutation_MissingRequiredBody_Returns400(string route)
    {
        // Arrange
        var lobby = await fixture.SeedAsync();
        var path = route == "create" ? "/api/lobbies" : $"/api/lobbies/{lobby.JoinCode}/join";

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, path, LobbyApiFixture.Token());

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 400);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mutation_AuthenticationOrScopeMissing_PrecedesMalformedInput(bool authenticated)
    {
        // Arrange
        var token = authenticated ? LobbyApiFixture.Token("unrelated.scope") : null;

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, "/api/lobbies/bad/join", token, "{");

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, authenticated ? 403 : 401);
    }

    [Fact]
    public async Task Mutation_QueryAndHeaderSpoofing_CannotOverrideTrustedSubjects()
    {
        // Arrange
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/lobbies?hostId=forged&userId=forged");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LobbyApiFixture.Token(subject: "Host"));
        createRequest.Headers.Add("X-User-Id", "forged");
        createRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var created = await fixture.Client.SendAsync(createRequest);
        var lobby = (await created.Content.ReadFromJsonAsync<LobbyResponse>())!;
        using var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/lobbies/{lobby.JoinCode}/join?playerId=forged");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LobbyApiFixture.Token(subject: "host"));
        joinRequest.Headers.Add("X-Player-Id", "forged");
        joinRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        using var joined = await fixture.Client.SendAsync(joinRequest);

        // Assert
        joined.StatusCode.Should().Be(HttpStatusCode.OK);
        var persisted = await fixture.Database.Repository().GetByJoinCodeAsync(lobby.JoinCode);
        persisted!.HostId.Should().Be("Host");
        persisted.Players.Select(player => player.PlayerId).Should().BeEquivalentTo("Host", "host");
    }

    internal static (HttpMethod Method, string Path, string Scope, ActorKind Actor) Route(string route, string code) => route switch
    {
        "preview" => (HttpMethod.Get, $"/api/lobbies/{code}", LobbyPolicies.LobbiesRead, ActorKind.User),
        "verify" => (HttpMethod.Get, $"/internal/lobbies/{code}", LobbyPolicies.LobbiesVerify, ActorKind.Machine),
        _ => (HttpMethod.Post, $"/api/lobbies/{code}/{route}", LobbyPolicies.LobbiesWrite, ActorKind.User),
    };
}
