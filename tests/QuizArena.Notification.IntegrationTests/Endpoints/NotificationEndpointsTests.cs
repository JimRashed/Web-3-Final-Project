using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using QuizArena.Notification.Api.Contracts;
using Xunit;

namespace QuizArena.Notification.IntegrationTests.Endpoints;

/// <summary>
/// Endpoint contract for the Notification API: routing, bearer validation, the machine/user split,
/// ownership and the status codes from the P02 contract.
/// </summary>
public sealed class NotificationEndpointsTests(NotificationApiFactory factory) : IClassFixture<NotificationApiFactory>
{
    private const string PlayerA = "player-a";
    private const string PlayerB = "player-b";

    [Fact]
    public async Task GetEvents_WithoutToken_Returns401()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/events");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostEvent_WithUserToken_Returns403BecauseIngestionIsMachineOnly()
    {
        // Arrange
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(factory.Tokens.UserToken(
            NotificationApiFactory.Audience, PlayerA, "events.write"));
        var body = new
        {
            eventId = Guid.NewGuid(),
            type = "MATCH_CREATED",
            userId = PlayerA,
            payload = new { matchId = "match-1" },
        };

        // Act
        using var response = await client.PostAsJsonAsync("/events", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostEvent_WithMachineToken_Returns202AndForwardsTheRequest()
    {
        // Arrange
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(factory.Tokens.MachineToken(
            NotificationApiFactory.Audience, "service-account-quizarena-gameplay", "events.write"));
        var eventId = Guid.NewGuid();
        var body = new
        {
            eventId,
            type = "MATCH_CREATED",
            userId = PlayerA,
            payload = new { matchId = "match-1" },
        };

        // Act
        using var response = await client.PostAsJsonAsync("/events", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        created!.EventId.Should().Be(eventId);
        created.Payload.MatchId.Should().Be("match-1");
        factory.Application.Published.Should().ContainSingle(request => request.EventId == eventId);
    }

    [Fact]
    public async Task PostEvent_WithUnknownPayloadField_Returns400()
    {
        // Arrange — the typed payload rejects members that are not part of the contract.
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(factory.Tokens.MachineToken(
            NotificationApiFactory.Audience, "service-account-quizarena-gameplay", "events.write"));

        // Act
        using var response = await client.PostAsync(
            "/events",
            new StringContent(
                """{"eventId":"26c94137-d660-4397-a731-050c2bb07004","type":"MATCH_CREATED","userId":"player-a","payload":{"matchId":"m1","extra":1}}""",
                System.Text.Encoding.UTF8,
                "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEvents_WithUserToken_UsesTheTokenSubject()
    {
        // Arrange
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(factory.Tokens.UserToken(
            NotificationApiFactory.Audience, PlayerA, "events.read"));

        // Act
        using var response = await client.GetAsync("/events");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Application.Subjects.Should().Contain(PlayerA);
    }

    [Fact]
    public async Task GetPreferences_ForAnotherPlayer_Returns403()
    {
        // Arrange
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(factory.Tokens.UserToken(
            NotificationApiFactory.Audience, PlayerA, "preferences.read"));

        // Act
        using var response = await client.GetAsync($"/users/{PlayerB}/preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPreferences_ForSelf_Returns200()
    {
        // Arrange
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(factory.Tokens.UserToken(
            NotificationApiFactory.Audience, PlayerA, "preferences.read"));

        // Act
        using var response = await client.GetAsync($"/users/{PlayerA}/preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preferences = await response.Content.ReadFromJsonAsync<UserPreferencesResponse>();
        preferences!.EmailEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePreferences_WithTheWriteScope_ReturnsTheStoredValues()
    {
        // Arrange
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(factory.Tokens.UserToken(
            NotificationApiFactory.Audience, PlayerA, "preferences.write"));

        // Act
        using var response = await client.PutAsJsonAsync(
            $"/users/{PlayerA}/preferences",
            new UpdatePreferencesRequest(true, true));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preferences = await response.Content.ReadFromJsonAsync<UserPreferencesResponse>();
        preferences!.EmailEnabled.Should().BeTrue();
        preferences.PushEnabled.Should().BeTrue();
    }

    private static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);
}
