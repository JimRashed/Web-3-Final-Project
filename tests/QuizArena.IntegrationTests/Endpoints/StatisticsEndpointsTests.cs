using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using QuizArena.Domain.Contracts;
using QuizArena.IntegrationTests.Infrastructure;

namespace QuizArena.IntegrationTests.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class StatisticsEndpointsTests : IntegrationTestBase
{
    public StatisticsEndpointsTests(QuizArenaWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Results_and_player_statistics_are_persisted()
    {
        using var client = Factory.CreateClient();
        var userId = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/results", new RecordResultRequest(Guid.NewGuid(), userId, 10, true))).StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await client.GetAsync($"/api/players/{userId}/stats");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<PlayerStatsDto>();
        stats.Should().Be(new PlayerStatsDto(userId, 1, 1, 10));

        var history = await client.GetFromJsonAsync<List<MatchHistoryDto>>($"/api/players/{userId}/matches");
        history.Should().ContainSingle().Which.Should().Match<MatchHistoryDto>(result =>
            result.MatchId != Guid.Empty && result.Score == 10 && result.Won);

        var leaderboard = await client.GetFromJsonAsync<List<LeaderboardEntryDto>>("/api/leaderboard?top=1");
        leaderboard.Should().ContainSingle().Which.Should().Be(new LeaderboardEntryDto(userId, 10, 1));

        var emptyUser = Guid.NewGuid();
        (await client.GetFromJsonAsync<PlayerStatsDto>($"/api/players/{emptyUser}/stats"))
            .Should().Be(new PlayerStatsDto(emptyUser, 0, 0, 0));
        (await client.GetFromJsonAsync<List<MatchHistoryDto>>($"/api/players/{emptyUser}/matches"))
            .Should().BeEmpty();
    }
}
