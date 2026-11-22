using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Notification.Api.Contracts;
using QuizArena.Notification.Api.Security;
using QuizArena.Notification.Data.Context;
using QuizArena.Notification.Domain.Entities;
using Xunit;

namespace QuizArena.Notification.IntegrationTests.Endpoints;

public sealed class NotificationRouteSecurityTests : IAsyncLifetime
{
    private const string PlayerA = "player-a";
    private const string PlayerB = "player-b";
    private const string Gameplay = "service-account-quizarena-gameplay";
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private const string ForbiddenTitle = "Insufficient scope, actor kind or ownership";

    private readonly DbContextOptions<NotificationDbContext> _options;
    private readonly NotificationApiFactory _factory;

    public NotificationRouteSecurityTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaNotification_RouteSecurity_{Guid.NewGuid():N}",
        };
        _options = new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer(connection.ConnectionString).Options;
        _factory = new NotificationApiFactory(connection.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        await using var context = new NotificationDbContext(_options);
        using var scope = _factory.Services.CreateScope();
        var hostConnection = scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.GetDbConnection();
        var testConnection = context.Database.GetDbConnection();
        hostConnection.Database.Should().Be(testConnection.Database);
        hostConnection.DataSource.Should().BeEquivalentTo(testConnection.DataSource);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await using var context = new NotificationDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }

    [Theory]
    [InlineData("publish", NotificationPolicies.EventsWrite, true)]
    [InlineData("publish", NotificationPolicies.EventsWrite, false)]
    [InlineData("list", NotificationPolicies.EventsRead, true)]
    [InlineData("list", NotificationPolicies.EventsRead, false)]
    [InlineData("get-preferences", NotificationPolicies.PreferencesRead, true)]
    [InlineData("get-preferences", NotificationPolicies.PreferencesRead, false)]
    [InlineData("put-preferences", NotificationPolicies.PreferencesWrite, true)]
    [InlineData("put-preferences", NotificationPolicies.PreferencesWrite, false)]
    public async Task NotificationRoute_WithWrongActorOrInexactScope_Returns403AndPreservesState(
        string route, string requiredScope, bool wrongActor)
    {
        // Arrange
        var isMachineRoute = route == "publish";
        var useMachineToken = isMachineRoute != wrongActor;
        var scope = wrongActor ? requiredScope : $"{requiredScope}.extra";
        // A subject matching the user route rules out ownership as the reason the machine was denied.
        using var client = useMachineToken
            ? MachineClient(scope, isMachineRoute ? Gameplay : PlayerA)
            : UserClient(scope);
        var before = await SnapshotAsync();

        // Act
        using var response = await SendAsync(client, route);

        // Assert
        await AssertForbiddenAsync(response);
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData("list", NotificationPolicies.EventsRead)]
    [InlineData("get-preferences", NotificationPolicies.PreferencesRead)]
    [InlineData("put-preferences", NotificationPolicies.PreferencesWrite)]
    public async Task UserRoute_ForAnotherPlayer_Returns403AndPreservesBothAccounts(string route, string scope)
    {
        // Arrange
        await SeedBothAccountsAsync();
        using var client = UserClient(scope);
        var before = await SnapshotAsync();

        // Act
        using var response = await SendAsync(client, route, PlayerB);

        // Assert
        await AssertForbiddenAsync(response);
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task UpdatePreferences_WithUndeclaredUserId_Returns400AndPreservesBothAccounts()
    {
        // Arrange
        await SeedBothAccountsAsync();
        using var client = UserClient(NotificationPolicies.PreferencesWrite);
        var before = await SnapshotAsync();

        // Act
        using var response = await client.PutAsJsonAsync($"/users/{PlayerA}/preferences",
            new { userId = PlayerB, emailEnabled = false, pushEnabled = false });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Publish_ByGameplay_PersistsTheEventForTheRequestedRecipient()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        using var client = MachineClient(NotificationPolicies.EventsWrite, Gameplay);

        // Act
        using var response = await client.PostAsJsonAsync("/events", EventBody(eventId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var stored = (await SnapshotAsync()).Events.Should().ContainSingle().Which;
        stored.EventId.Should().Be(eventId);
        stored.Issuer.Should().Be(_factory.Tokens.Issuer);
        stored.UserId.Should().Be(PlayerA);
        stored.MatchId.Should().Be("match-1");
    }

    [Fact]
    public async Task ListEvents_ForSelf_ReturnsOnlyTheTokenSubjectsEvents()
    {
        // Arrange
        await SeedBothAccountsAsync();
        using var client = UserClient(NotificationPolicies.EventsRead);

        // Act
        using var response = await SendAsync(client, "list", PlayerA);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<EventResponse[]>();
        events.Should().ContainSingle().Which.UserId.Should().Be(PlayerA);
    }

    [Fact]
    public async Task GetPreferences_ForSelfWithoutARow_ReturnsDefaultsWithoutInserting()
    {
        // Arrange
        using var client = UserClient(NotificationPolicies.PreferencesRead);

        // Act
        using var response = await SendAsync(client, "get-preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserPreferencesResponse>())
            .Should().Be(new UserPreferencesResponse(false, false));
        (await SnapshotAsync()).Preferences.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdatePreferences_ForSelf_PersistsOnlyTheTokenSubjectsPreferences()
    {
        // Arrange
        using var client = UserClient(NotificationPolicies.PreferencesWrite);

        // Act
        using var response = await SendAsync(client, "put-preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = (await SnapshotAsync()).Preferences.Should().ContainSingle().Which;
        stored.Issuer.Should().Be(_factory.Tokens.Issuer);
        stored.Subject.Should().Be(PlayerA);
        stored.EmailEnabled.Should().BeTrue();
        stored.PushEnabled.Should().BeTrue();
    }

    private HttpClient UserClient(string scope) => Client(_factory.Tokens.UserToken(NotificationApiFactory.Audience, PlayerA, scope));

    private HttpClient MachineClient(string scope, string subject) =>
        Client(_factory.Tokens.MachineToken(NotificationApiFactory.Audience, subject, scope));

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object EventBody(Guid eventId) => new
    {
        eventId,
        type = "MATCH_CREATED",
        userId = PlayerA,
        payload = new { matchId = "match-1" },
    };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string route, string? userId = null) =>
        route switch
        {
            "publish" => client.PostAsJsonAsync("/events", EventBody(Guid.NewGuid())),
            "list" => client.GetAsync(userId is null ? "/events" : $"/events?userId={userId}"),
            "get-preferences" => client.GetAsync($"/users/{userId ?? PlayerA}/preferences"),
            "put-preferences" => client.PutAsJsonAsync($"/users/{userId ?? PlayerA}/preferences", new UpdatePreferencesRequest(true, true)),
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };

    private async Task SeedBothAccountsAsync()
    {
        await using var context = new NotificationDbContext(_options);
        var issuer = _factory.Tokens.Issuer;
        context.Events.AddRange(Event.CreateMatchCreated(Guid.NewGuid(), issuer, PlayerA, "match-a", DateTime.UtcNow),
            Event.CreateMatchCreated(Guid.NewGuid(), issuer, PlayerB, "match-b", DateTime.UtcNow));
        context.UserPreferences.AddRange(UserPreference.Rehydrate(issuer, PlayerA, true, false),
            UserPreference.Rehydrate(issuer, PlayerB, false, true));
        await context.SaveChangesAsync();
    }

    private async Task<StoredState> SnapshotAsync()
    {
        await using var context = new NotificationDbContext(_options);
        return new StoredState(await context.Events.AsNoTracking().ToArrayAsync(),
            await context.UserPreferences.AsNoTracking().ToArrayAsync());
    }

    private static async Task AssertForbiddenAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.WwwAuthenticate.Should().BeEmpty();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.EnumerateObject().Select(field => field.Name)
            .Should().BeEquivalentTo("type", "title", "status", "detail", "traceId");
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("title").GetString().Should().Be(ForbiddenTitle);
        problem.GetProperty("detail").GetString().Should().Be(ForbiddenTitle);
        problem.GetProperty("type").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private sealed record StoredState(Event[] Events, UserPreference[] Preferences);
}
