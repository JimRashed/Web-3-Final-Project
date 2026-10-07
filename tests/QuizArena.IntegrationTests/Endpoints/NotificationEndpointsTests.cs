using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using QuizArena.Domain.Contracts;
using QuizArena.IntegrationTests.Infrastructure;

namespace QuizArena.IntegrationTests.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class NotificationEndpointsTests(QuizArenaWebApplicationFactory factory)
{
    [Fact]
    public async Task Events_and_preferences_are_available_without_authentication()
    {
        using var client = factory.CreateClient();
        var userId = $"user-{Guid.NewGuid():N}";
        (await client.PostAsJsonAsync("/events", new CreateEventRequest("TEST", userId, "{}"))).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await client.GetAsync($"/events?userId={userId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/users/{userId}/preferences", new UpdatePrefRequest(true, false))).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
