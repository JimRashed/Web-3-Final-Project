using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using QuizArena.Domain.Contracts;
using QuizArena.IntegrationTests.Infrastructure;

namespace QuizArena.IntegrationTests.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class StatisticsEndpointsTests(QuizArenaWebApplicationFactory factory)
{
    [Fact]
    public async Task Results_and_player_statistics_are_persisted()
    {
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/results", new RecordResultRequest(Guid.NewGuid(), userId, 10, true))).StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await client.GetAsync($"/api/players/{userId}/stats");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<PlayerStatsDto>();
        stats!.GamesPlayed.Should().Be(1);
        (await client.GetAsync("/api/leaderboard?top=1")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/players/{userId}/matches")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/players/{Guid.NewGuid()}/matches")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/leaderboard?top=1")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
