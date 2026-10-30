using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Services;
using QuizArena.IntegrationTests.Infrastructure;

namespace QuizArena.IntegrationTests.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class NotificationEndpointsTests : IntegrationTestBase
{
    public NotificationEndpointsTests(QuizArenaWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Events_and_preferences_are_available_without_authentication()
    {
        using var client = Factory.CreateClient();
        var userId = $"user-{Guid.NewGuid():N}";
        var created = await client.PostAsJsonAsync("/events", new CreateEventRequest("TEST", userId, "{}"));
        created.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var eventPayload = await created.Content.ReadFromJsonAsync<EventResponse>();
        eventPayload.Should().NotBeNull();
        eventPayload!.Type.Should().Be("TEST");
        eventPayload.UserId.Should().Be(userId);
        eventPayload.PayloadJson.Should().Be("{}");

        var events = await client.GetFromJsonAsync<List<EventResponse>>($"/events?userId={userId}");
        events.Should().ContainSingle().Which.Should().BeEquivalentTo(eventPayload);

        var preference = await client.PutAsJsonAsync($"/users/{userId}/preferences", new UpdatePrefRequest(true, false));
        preference.StatusCode.Should().Be(HttpStatusCode.OK);
        var preferencePayload = await preference.Content.ReadFromJsonAsync<UserPrefDto>();
        preferencePayload.Should().Be(new UserPrefDto(userId, true, false));
    }
}
