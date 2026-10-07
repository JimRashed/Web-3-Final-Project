using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using QuizArena.Domain.Contracts;
using QuizArena.IntegrationTests.Infrastructure;

namespace QuizArena.IntegrationTests.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class MatchEndpointsTests : IntegrationTestBase
{
    public MatchEndpointsTests(QuizArenaWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Match_lifecycle_returns_created_and_persisted_state()
    {
        using var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/matches", new CreateMatchRequest($"match-{Guid.NewGuid():N}", "a", "b"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var match = await response.Content.ReadFromJsonAsync<MatchResponse>();
        match.Should().NotBeNull();
        (await client.GetAsync($"/matches/{match!.MatchId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/matches/{match.MatchId}/next-question")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/matches/{match.MatchId}/answers", new SubmitAnswerRequest("a", "q-1", "H2O"))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Missing_match_returns_not_found()
    {
        using var client = Factory.CreateClient();
        (await client.GetAsync("/matches/missing")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
