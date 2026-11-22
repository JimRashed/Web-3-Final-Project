using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Data.Context;
using QuizArena.Gameplay.Data.Records;
using QuizArena.Gameplay.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Endpoints;

public sealed class GameplayRouteSecurityTests : IAsyncLifetime
{
    private const string Host = "player-a";
    private const string Guest = "player-b";
    private const string Outsider = "player-c";
    private const string JoinCode = "ABC123";
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private const string ForbiddenTitle = "Insufficient scope, actor kind or ownership";

    private readonly DbContextOptions<GameplayDbContext> _options;
    private readonly GameplayApiFactory _factory;

    public GameplayRouteSecurityTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaGameplay_RouteSecurity_{Guid.NewGuid():N}",
        };
        _options = new DbContextOptionsBuilder<GameplayDbContext>().UseSqlServer(connection.ConnectionString).Options;
        _factory = new GameplayApiFactory(connection.ConnectionString);
        _factory.Lobby.State = new LobbyState("lobby-1", Host, [Host, Guest], IsLocked: true);
    }

    public async Task InitializeAsync()
    {
        await using var context = new GameplayDbContext(_options);
        using var scope = _factory.Services.CreateScope();
        var hostConnection = scope.ServiceProvider.GetRequiredService<GameplayDbContext>().Database.GetDbConnection();
        var testConnection = context.Database.GetDbConnection();
        hostConnection.Database.Should().Be(testConnection.Database);
        hostConnection.DataSource.Should().BeEquivalentTo(testConnection.DataSource);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await using var context = new GameplayDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }

    [Theory]
    [InlineData("create", GameplayPolicies.MatchesWrite, true)]
    [InlineData("create", GameplayPolicies.MatchesWrite, false)]
    [InlineData("get", GameplayPolicies.MatchesRead, true)]
    [InlineData("get", GameplayPolicies.MatchesRead, false)]
    [InlineData("next", GameplayPolicies.MatchesRead, true)]
    [InlineData("next", GameplayPolicies.MatchesRead, false)]
    [InlineData("answer", GameplayPolicies.MatchesWrite, true)]
    [InlineData("answer", GameplayPolicies.MatchesWrite, false)]
    public async Task MatchRoute_WithWrongActorOrInexactScope_Returns403AndPreservesState(
        string route, string requiredScope, bool wrongActor)
    {
        // Arrange
        var matchId = route == "create" ? null : await CreateMatchAsync();
        var questionId = route == "answer" ? await CurrentQuestionAsync(matchId!) : Guid.Empty;
        var actor = wrongActor ? ActorKind.Machine : ActorKind.User;
        var scope = wrongActor ? requiredScope : $"{requiredScope}.extra";
        // The subject can pass ownership so the denial specifically exercises route authorization.
        using var client = _factory.Client(TestTokens.Create(actor, scope, claims => claims["sub"] = Host));
        var before = await SnapshotAsync();

        // Act
        using var response = await SendAsync(client, route, matchId, questionId);

        // Assert
        await AssertForbiddenAsync(response);
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Create_ByNonHost_Returns403BeforeMatchOrLockState(bool locked, bool alreadyCreated)
    {
        // Arrange
        if (alreadyCreated)
        {
            await CreateMatchAsync();
        }

        _factory.Lobby.State = _factory.Lobby.State! with { IsLocked = locked };
        using var client = UserClient(GameplayPolicies.MatchesWrite, Guest);
        var before = await SnapshotAsync();

        // Act
        using var response = await SendAsync(client, "create");

        // Assert
        await AssertForbiddenAsync(response);
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Create_WithHostSubjectFromDifferentIssuer_Returns401WithoutWriting()
    {
        // Arrange
        var token = TestTokens.Create(ActorKind.User, GameplayPolicies.MatchesWrite, claims =>
        {
            claims["sub"] = Host;
            claims["iss"] = "http://localhost:8080/realms/other";
        });
        using var client = _factory.Client(token);
        var before = await SnapshotAsync();

        // Act
        using var response = await SendAsync(client, "create");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle(challenge => challenge.Scheme == "Bearer");
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData("abc123", HttpStatusCode.BadRequest)]
    [InlineData("ZZZ999", HttpStatusCode.NotFound)]
    public async Task Create_WithInvalidOrUnknownJoinCode_ReturnsTheExpectedErrorWithoutWriting(
        string joinCode, HttpStatusCode expectedStatus)
    {
        // Arrange
        using var client = UserClient(GameplayPolicies.MatchesWrite);
        var before = await SnapshotAsync();

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", new { joinCode });

        // Assert
        response.StatusCode.Should().Be(expectedStatus);
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData("get", GameplayPolicies.MatchesRead)]
    [InlineData("next", GameplayPolicies.MatchesRead)]
    [InlineData("answer", GameplayPolicies.MatchesWrite)]
    public async Task MatchRoute_ByOutsider_Returns403AndPreservesState(string route, string scope)
    {
        // Arrange
        var matchId = await CreateMatchAsync();
        var questionId = await CurrentQuestionAsync(matchId);
        using var client = UserClient(scope, Outsider);
        var before = await SnapshotAsync();

        // Act
        using var response = await SendAsync(client, route, matchId, questionId);

        // Assert
        await AssertForbiddenAsync(response);
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData("get", GameplayPolicies.MatchesRead)]
    [InlineData("next", GameplayPolicies.MatchesRead)]
    [InlineData("answer", GameplayPolicies.MatchesWrite)]
    public async Task MatchRoute_WithUnknownMatch_Returns404AndPreservesState(string route, string scope)
    {
        // Arrange
        using var client = UserClient(scope);
        var before = await SnapshotAsync();

        // Act
        using var response = await SendAsync(client, route, "unknown", Guid.NewGuid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData("create", "hostId")]
    [InlineData("answer", "playerId")]
    public async Task MatchMutation_WithAnIdentityField_Returns400AndPreservesState(string route, string identityField)
    {
        // Arrange
        using var client = UserClient(GameplayPolicies.MatchesWrite, Guest);
        var path = "/matches";
        var body = new Dictionary<string, object> { [identityField] = Host };
        if (route == "answer")
        {
            var matchId = await CreateMatchAsync();
            path = $"/matches/{matchId}/answers";
            body["questionId"] = await CurrentQuestionAsync(matchId);
            body["answer"] = "A";
        }
        else
        {
            body["joinCode"] = JoinCode;
        }

        var before = await SnapshotAsync();

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, path, body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Answer_ByParticipant_PersistsTheAnswerUnderTheTokenSubject()
    {
        // Arrange
        var matchId = await CreateMatchAsync();
        var questionId = await CurrentQuestionAsync(matchId);
        using var client = UserClient(GameplayPolicies.MatchesWrite, Guest);
        var before = await SnapshotAsync();

        // Act
        using var response = await SendAsync(client, "answer", matchId, questionId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await SnapshotAsync();
        var match = after.Matches.Should().ContainSingle().Which;
        match.PlayerAId.Should().Be(Host);
        match.PlayerBId.Should().Be(Guest);
        using var answers = JsonDocument.Parse(match.AnswersJson);
        var storedAnswer = answers.RootElement.EnumerateArray().Should().ContainSingle().Which;
        storedAnswer.GetProperty("userId").GetString().Should().Be(Guest);
        storedAnswer.GetProperty("questionId").GetGuid().Should().Be(questionId);
        after.Deliveries.Should().NotBeEmpty().And.BeEquivalentTo(before.Deliveries);
    }

    private HttpClient UserClient(string scope, string subject = Host) =>
        _factory.Client(TestTokens.Create(ActorKind.User, scope, claims => claims["sub"] = subject));

    private async Task<string> CreateMatchAsync()
    {
        using var client = UserClient(GameplayPolicies.MatchesWrite);
        using var response = await SendAsync(client, "create");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var match = await GameplayHttp.JsonAsync(response);
        return match.RootElement.GetProperty("matchId").GetString()!;
    }

    private async Task<Guid> CurrentQuestionAsync(string matchId)
    {
        using var client = UserClient(GameplayPolicies.MatchesRead);
        using var response = await SendAsync(client, "next", matchId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var question = await GameplayHttp.JsonAsync(response);
        return question.RootElement.GetProperty("questionId").GetGuid();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string route, string? matchId = null, Guid questionId = default) =>
        route switch
        {
            "create" => GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", new { joinCode = JoinCode }),
            "get" => GameplayHttp.SendAsync(client, HttpMethod.Get, $"/matches/{matchId}"),
            "next" => GameplayHttp.SendAsync(client, HttpMethod.Get, $"/matches/{matchId}/next-question"),
            "answer" => GameplayHttp.SendAsync(client, HttpMethod.Post, $"/matches/{matchId}/answers", new { questionId, answer = "A" }),
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };

    private async Task<StoredState> SnapshotAsync()
    {
        await using var context = new GameplayDbContext(_options);
        return new StoredState(await context.Matches.AsNoTracking().ToArrayAsync(), await context.Deliveries.AsNoTracking().ToArrayAsync());
    }

    private static async Task AssertForbiddenAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.WwwAuthenticate.Should().BeEmpty();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var json = await GameplayHttp.JsonAsync(response);
        var problem = json.RootElement;
        problem.EnumerateObject().Select(field => field.Name)
            .Should().BeEquivalentTo("type", "title", "status", "detail", "traceId");
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("title").GetString().Should().Be(ForbiddenTitle);
        problem.GetProperty("detail").GetString().Should().Be(ForbiddenTitle);
        problem.GetProperty("type").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private sealed record StoredState(MatchRecord[] Matches, DeliveryRecord[] Deliveries);
}
