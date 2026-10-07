using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using QuizArena.Domain.Contracts;
using QuizArena.IntegrationTests.Infrastructure;

namespace QuizArena.IntegrationTests.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class LobbyEndpointsTests : IntegrationTestBase
{
    public LobbyEndpointsTests(QuizArenaWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Missing_lobby_returns_not_found()
    {
        using var client = Factory.CreateClient();
        (await client.PostAsync("/api/lobbies/missing/lock", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Existing_lobby_is_locked_and_persisted()
    {
        using var client = Factory.CreateClient();
        var response = await client.PostAsync($"/api/lobbies/{Factory.TestJoinCode}/lock", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<LobbyProvisionResponse>();
        payload.Should().NotBeNull();
        payload!.JoinCode.Should().Be(Factory.TestJoinCode);
        payload.Status.Should().Be("Locked");
        payload.PlayerIds.Should().ContainSingle().Which.Should().Be("host");

        var persisted = await client.PostAsync($"/api/lobbies/{Factory.TestJoinCode}/lock", null);
        var persistedPayload = await persisted.Content.ReadFromJsonAsync<LobbyProvisionResponse>();
        persistedPayload.Should().BeEquivalentTo(payload);
    }
}
