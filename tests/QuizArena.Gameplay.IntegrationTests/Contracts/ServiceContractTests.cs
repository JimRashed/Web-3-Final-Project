using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Api.Services.Outbox;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Repositories;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Contracts;

/// <summary>
/// The machine-facing contract Gameplay depends on (D1, #146). Every assertion about a route, a scope or a
/// field is made against the committed OpenAPI document, so a rename on either side fails this suite
/// instead of failing a live demo.
/// </summary>
public sealed class ServiceContractTests
{
    // (contract file, path, verb, scope) — the four routes Gameplay calls as a machine client.
    public static TheoryData<string, string, string, string> MachineRoutes() => new()
    {
        { "lobby.yaml", "/internal/lobbies/{joinCode}", "get", "lobbies.verify" },
        { "questionbank.yaml", "/internal/question-sets", "post", "questions.play" },
        { "statistics.yaml", "/api/results", "post", "results.write" },
        { "notification.yaml", "/events", "post", "events.write" },
    };

    [Theory]
    [MemberData(nameof(MachineRoutes))]
    public void MachineRoute_IsDocumentedAsMachineWithTheScopeGameplayUses(
        string contract, string path, string verb, string scope)
    {
        // Arrange
        using var document = ReadContract(contract);

        // Act
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(verb);

        // Assert
        operation.GetProperty("x-actor").GetString().Should().Be("machine");
        operation.GetProperty("x-policy").GetString().Should().Be(scope);
        operation.GetProperty("x-audience").GetString()
            .Should().Be($"quizarena-{contract.Replace(".yaml", string.Empty, StringComparison.Ordinal)}");
    }

    [Fact]
    public async Task LobbyAdapter_CallsTheDocumentedRouteWithTheMachineToken()
    {
        // Arrange
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """
            {"lobbyId":"8d1c6c4e-2f7a-4a3e-9a51-0b7f0f1f2a3b","joinCode":"ABC123","status":"Locked","hostUserId":"player-a","playerIds":["player-a","player-b"]}
            """));
        var tokens = new FakeTokenSource();
        var gateway = new LobbyHttpGateway(
            new HttpClient(handler), tokens, Options.Create(Services()), NullLogger<LobbyHttpGateway>.Instance);

        // Act
        var lobby = await gateway.GetAsync("ABC123", CancellationToken.None);

        // Assert
        lobby!.IsLocked.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].Uri.AbsolutePath.Should().Be("/internal/lobbies/ABC123");
        handler.Requests[0].Authorization.Should().Be("Bearer machine-token");
        tokens.RequestedScopes.Should().Equal("lobbies.verify");
    }

    [Fact]
    public async Task LobbyAdapter_UnknownJoinCodeIsNotAnError()
    {
        // Arrange
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, "{}"));
        var gateway = new LobbyHttpGateway(
            new HttpClient(handler), new FakeTokenSource(), Options.Create(Services()), NullLogger<LobbyHttpGateway>.Instance);

        // Act
        var lobby = await gateway.GetAsync("ZZZ999", CancellationToken.None);

        // Assert
        lobby.Should().BeNull();
    }

    [Theory]
    [InlineData(403)]
    [InlineData(500)]
    public async Task LobbyAdapter_WhenTheReceiverRejectsOrFails_Is503(int status)
    {
        // Arrange
        var handler = new RecordingHandler(_ => Json((HttpStatusCode)status, "{}"));
        var gateway = new LobbyHttpGateway(
            new HttpClient(handler), new FakeTokenSource(), Options.Create(Services()), NullLogger<LobbyHttpGateway>.Instance);

        // Act
        var act = async () => await gateway.GetAsync("ABC123", CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task QuestionBankAdapter_SendsTheBodyTheContractRequires()
    {
        // Arrange
        using var document = ReadContract("questionbank.yaml");
        var requestSchema = document.RootElement.GetProperty("paths").GetProperty("/internal/question-sets")
            .GetProperty("post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        // The body is usually a $ref; resolve it so the assertion is about the actual schema.
        var resolved = requestSchema.TryGetProperty("$ref", out var reference)
            ? document.RootElement.GetProperty("components").GetProperty("schemas")
                .GetProperty(reference.GetString()!.Split('/')[^1])
            : requestSchema;
        resolved.GetProperty("properties").TryGetProperty("count", out _).Should().BeTrue();

        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, QuestionSetJson(5)));
        var catalog = new QuestionBankHttpClient(
            new HttpClient(handler), new FakeTokenSource(), Options.Create(Services()), NullLogger<QuestionBankHttpClient>.Instance);

        // Act
        var snapshots = await catalog.SnapshotAsync(5, CancellationToken.None);

        // Assert
        snapshots.Should().HaveCount(5);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].Uri.AbsolutePath.Should().Be("/internal/question-sets");
        handler.Requests[0].Body.Should().Be("{\"count\":5}");
        handler.Requests[0].Authorization.Should().Be("Bearer machine-token");
    }

    [Fact]
    public async Task QuestionBankAdapter_WhenTheCatalogCannotSupplyTheSet_FailsClosedWith503()
    {
        // Arrange
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, "{}"));
        var catalog = new QuestionBankHttpClient(
            new HttpClient(handler), new FakeTokenSource(), Options.Create(Services()), NullLogger<QuestionBankHttpClient>.Instance);

        // Act
        var act = async () => await catalog.SnapshotAsync(5, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task StatisticsChannel_PostsTheFieldsTheResultContractDefines()
    {
        // Arrange
        using var document = ReadContract("statistics.yaml");
        var fields = SchemaFields(document, "RecordResult");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var channel = new StatisticsResultChannel(
            new FakeClientFactory(handler), new FakeTokenSource(), Options.Create(Services()),
            NullLogger<StatisticsResultChannel>.Instance);

        // Act
        var attempt = await channel.SendAsync(ResultPayload(), CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Delivered);
        var body = JsonDocument.Parse(handler.Requests[0].Body!).RootElement;
        body.EnumerateObject().Select(property => property.Name).Should().BeSubsetOf(fields);
        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("matchId", "userId", "score", "won", "playedAt");
        handler.Requests[0].Uri.AbsolutePath.Should().Be("/api/results");
        handler.Requests[0].Authorization.Should().Be("Bearer machine-token");
    }

    [Fact]
    public async Task NotificationChannel_PostsTheFieldsTheEventContractDefines()
    {
        // Arrange
        using var document = ReadContract("notification.yaml");
        var fields = SchemaFields(document, "CreateEvent");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Accepted, "{}"));
        var channel = new NotificationEventChannel(
            new FakeClientFactory(handler), new FakeTokenSource(), Options.Create(Services()),
            NullLogger<NotificationEventChannel>.Instance);

        // Act
        var attempt = await channel.SendAsync(EventPayload(), CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Delivered);
        var body = JsonDocument.Parse(handler.Requests[0].Body!).RootElement;
        body.EnumerateObject().Select(property => property.Name).Should().BeSubsetOf(fields);
        body.GetProperty("type").GetString().Should().Be("MATCH_COMPLETED");
        handler.Requests[0].Uri.AbsolutePath.Should().Be("/events");
        handler.Requests[0].Authorization.Should().Be("Bearer machine-token");
    }

    [Theory]
    [InlineData(400)]
    [InlineData(409)]
    public async Task StatisticsChannel_TreatsAReceiverRejectionAsPermanent(int status)
    {
        // Arrange
        var handler = new RecordingHandler(_ => Json((HttpStatusCode)status, "{}"));
        var channel = new StatisticsResultChannel(
            new FakeClientFactory(handler), new FakeTokenSource(), Options.Create(Services()),
            NullLogger<StatisticsResultChannel>.Instance);

        // Act
        var attempt = await channel.SendAsync(ResultPayload(), CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Permanent);
    }

    private static readonly Guid[] QuestionIds =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
    ];

    private static Match FinishedMatch()
    {
        var match = Match.Create(
            "match-1", "lobby-1", "player-a", "player-b",
            QuestionIds.Select(id => QuestionSnapshot.Create(id, "Question?", ["A", "B", "C", "D"], "A")).ToArray(),
            DateTime.UtcNow);

        for (var round = 0; round < Match.RequiredQuestionCount; round++)
        {
            match.SubmitAnswer("player-a", QuestionIds[round], "A", DateTime.UtcNow);
            match.SubmitAnswer("player-b", QuestionIds[round], "B", DateTime.UtcNow);
        }

        return match;
    }

    private static string ResultPayload() =>
        MatchDeliveries.OnCompleted(FinishedMatch())
            .First(delivery => delivery.Destination == MatchDelivery.Statistics).PayloadJson;

    private static string EventPayload() =>
        MatchDeliveries.OnCompleted(FinishedMatch())
            .First(delivery => delivery.Destination == MatchDelivery.Notification).PayloadJson;

    private static string QuestionSetJson(int count) =>
        "[" + string.Join(",", Enumerable.Range(0, count).Select(index => $$"""
            {"id":"0000000{{index}}-1111-2222-3333-444444444444","prompt":"Question {{index}}?","category":"general","options":["A","B","C","D"],"correctAnswer":"A"}
            """)) + "]";

    private static IEnumerable<string> SchemaFields(JsonDocument document, string schema) =>
        document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schema)
            .GetProperty("properties").EnumerateObject().Select(property => property.Name);

    private static JsonDocument ReadContract(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new DirectoryNotFoundException("QuizArena.sln not found.");
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "contracts", "openapi", name)));
    }

    private static GameplayServiceOptions Services() => new()
    {
        Lobby = "http://lobby.test",
        QuestionBank = "http://questionbank.test",
        Statistics = "http://statistics.test",
        Notification = "http://notification.test",
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Body);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }

    private sealed class FakeClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeTokenSource : IMachineTokenSource
    {
        public List<string> RequestedScopes { get; } = [];

        public Task<string> GetAsync(string scope, CancellationToken cancellationToken)
        {
            RequestedScopes.Add(scope);
            return Task.FromResult("machine-token");
        }

        public void Invalidate(string scope)
        {
        }
    }
}
