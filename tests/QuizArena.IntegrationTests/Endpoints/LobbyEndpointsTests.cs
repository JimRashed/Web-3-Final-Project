using System.Net;
using FluentAssertions;
using QuizArena.IntegrationTests.Infrastructure;

namespace QuizArena.IntegrationTests.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class LobbyEndpointsTests(QuizArenaWebApplicationFactory factory)
{
    [Fact]
    public async Task Missing_lobby_returns_not_found()
    {
        using var client = factory.CreateClient();
        (await client.PostAsync("/api/lobbies/missing/lock", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Existing_lobby_is_locked_and_persisted()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync($"/api/lobbies/{factory.TestJoinCode}/lock", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("Locked");
    }
}
