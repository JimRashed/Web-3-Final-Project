using System.Net;
using FluentAssertions;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Endpoints;

/// <summary>
/// The real <c>/matches</c> routes over HTTP: JWT validation, named policies, participant checks,
/// status codes, DTO shape and the fail-closed behaviour when a dependency is down.
/// </summary>
public sealed class MatchesEndpointTests
{
    private const string Host = "player-a";
    private const string Guest = "player-b";
    private const string Stranger = "player-x";
    private const string LobbyId = "lobby-1";

    private static string WriteToken(string subject = Host) =>
        TestTokens.Create(ActorKind.User, GameplayPolicies.MatchesWrite, claims => claims["sub"] = subject);

    private static string ReadToken(string subject = Host) =>
        TestTokens.Create(ActorKind.User, GameplayPolicies.MatchesRead, claims => claims["sub"] = subject);

    private static GameplayApiFactory Factory(bool locked = true, params string[] members)
    {
        var factory = new GameplayApiFactory();
        factory.Lobby.State = new LobbyState(
            LobbyId,
            Host,
            members.Length == 0 ? [Host, Guest] : members,
            locked);

        return factory;
    }

    private static object CreateBody(string joinCode = "ABC123") => new { joinCode };

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        // Arrange
        using var factory = Factory();
        using var client = factory.Client(token: null);

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().StartWith("Bearer");
        factory.Matches.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WithReadScopeOnly_Returns403()
    {
        // Arrange
        using var factory = Factory();
        using var client = factory.Client(ReadToken());

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        factory.Matches.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ByHost_Returns201AndTheNewMatch()
    {
        // Arrange
        using var factory = Factory();
        using var client = factory.Client(WriteToken());

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());
        using var json = await GameplayHttp.JsonAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/matches/");
        json.RootElement.GetProperty("status").GetString().Should().Be("ACTIVE");
        json.RootElement.GetProperty("playerAId").GetString().Should().Be(Host);
        json.RootElement.GetProperty("playerBId").GetString().Should().Be(Guest);
        json.RootElement.GetProperty("lobbyId").GetString().Should().Be(LobbyId);
        factory.Matches.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_RepeatedByHost_Returns200WithTheSameMatch()
    {
        // Arrange
        using var factory = Factory();
        using var client = factory.Client(WriteToken());
        var matchId = await CreateMatchAsync(client);

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());
        using var json = await GameplayHttp.JsonAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("matchId").GetString().Should().Be(matchId);
        factory.Matches.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_WithUnknownJoinCode_Returns404()
    {
        // Arrange
        using var factory = Factory();
        using var client = factory.Client(WriteToken());

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody("ZZZ999"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        factory.Matches.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ByANonHost_Returns403()
    {
        // Arrange
        using var factory = Factory();
        using var client = factory.Client(WriteToken(Guest));

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        factory.Matches.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WhenTheLobbyIsNotLocked_Returns409()
    {
        // Arrange
        using var factory = Factory(locked: false);
        using var client = factory.Client(WriteToken());

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WhenTheLobbyHasThreeMembers_Returns409()
    {
        // Arrange
        using var factory = Factory(locked: true, Host, Guest, Stranger);
        using var client = factory.Client(WriteToken());

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WhenTheLobbyServiceFails_Returns503AndPersistsNoMatch()
    {
        // Arrange
        using var factory = Factory();
        factory.Lobby.Failure = new GameplayRequestException(503, "lobby down");
        using var client = factory.Client(WriteToken());

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        factory.Matches.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WhenTheQuestionBankCannotSupplyTheSet_Returns503AndPersistsNoMatch()
    {
        // Arrange
        using var factory = Factory();
        factory.Catalog.Count = 4;
        using var client = factory.Client(WriteToken());

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        factory.Matches.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_ByParticipant_ReturnsTheMatch()
    {
        // Arrange
        using var factory = Factory();
        using var writer = factory.Client(WriteToken());
        using var reader = factory.Client(ReadToken());
        var matchId = await CreateMatchAsync(writer);

        // Act
        using var response = await GameplayHttp.SendAsync(reader, HttpMethod.Get, $"/matches/{matchId}");
        using var json = await GameplayHttp.JsonAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("matchId").GetString().Should().Be(matchId);
    }

    [Fact]
    public async Task Get_ByANonParticipant_Returns403()
    {
        // Arrange
        using var factory = Factory();
        using var writer = factory.Client(WriteToken());
        var matchId = await CreateMatchAsync(writer);
        using var stranger = factory.Client(ReadToken(Stranger));

        // Act
        using var response = await GameplayHttp.SendAsync(stranger, HttpMethod.Get, $"/matches/{matchId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_WithAnUnknownId_Returns404()
    {
        // Arrange
        using var factory = Factory();
        using var reader = factory.Client(ReadToken());

        // Act
        using var response = await GameplayHttp.SendAsync(reader, HttpMethod.Get, "/matches/does-not-exist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NextQuestion_ReturnsOnlyThePublicFields()
    {
        // Arrange
        using var factory = Factory();
        using var writer = factory.Client(WriteToken());
        using var reader = factory.Client(ReadToken());
        var matchId = await CreateMatchAsync(writer);

        // Act
        using var response = await GameplayHttp.SendAsync(reader, HttpMethod.Get, $"/matches/{matchId}/next-question");
        using var json = await GameplayHttp.JsonAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("questionId", "text", "options");
        json.RootElement.GetProperty("options").GetArrayLength().Should().Be(4);
        response.Content.Headers.ContentType!.ToString().Should().StartWith("application/json");
    }

    [Fact]
    public async Task NextQuestion_WhenTheMatchIsFinished_Returns409()
    {
        // Arrange
        using var factory = Factory();
        using var host = factory.Client(WriteToken());
        using var guest = factory.Client(WriteToken(Guest));
        using var reader = factory.Client(ReadToken());
        var matchId = await CreateMatchAsync(host);

        for (var round = 0; round < Match.RequiredQuestionCount; round++)
        {
            var questionId = await CurrentQuestionAsync(reader, matchId);
            await AnswerAsync(host, matchId, questionId);
            await AnswerAsync(guest, matchId, questionId);
        }

        // Act
        using var response = await GameplayHttp.SendAsync(reader, HttpMethod.Get, $"/matches/{matchId}/next-question");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Answer_WithAnOptionOutsideTheQuestion_Returns400()
    {
        // Arrange
        using var factory = Factory();
        using var client = factory.Client(WriteToken());
        using var reader = factory.Client(ReadToken());
        var matchId = await CreateMatchAsync(client);
        var questionId = await CurrentQuestionAsync(reader, matchId);

        // Act
        using var response = await GameplayHttp.SendAsync(
            client,
            HttpMethod.Post,
            $"/matches/{matchId}/answers",
            new { questionId, answer = "Z" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Answer_Twice_Returns409()
    {
        // Arrange
        using var factory = Factory();
        using var client = factory.Client(WriteToken());
        using var reader = factory.Client(ReadToken());
        var matchId = await CreateMatchAsync(client);
        var questionId = await CurrentQuestionAsync(reader, matchId);
        await AnswerAsync(client, matchId, questionId);

        // Act
        using var response = await GameplayHttp.SendAsync(
            client,
            HttpMethod.Post,
            $"/matches/{matchId}/answers",
            new { questionId, answer = "A" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Answer_ByANonParticipant_Returns403()
    {
        // Arrange
        using var factory = Factory();
        using var host = factory.Client(WriteToken());
        using var hostReader = factory.Client(ReadToken());
        var matchId = await CreateMatchAsync(host);
        var questionId = await CurrentQuestionAsync(hostReader, matchId);
        using var stranger = factory.Client(WriteToken(Stranger));

        // Act
        using var response = await GameplayHttp.SendAsync(
            stranger,
            HttpMethod.Post,
            $"/matches/{matchId}/answers",
            new { questionId, answer = "A" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Answer_ByAParticipant_ReturnsTheOutcomeAndScores()
    {
        // Arrange
        using var factory = Factory();
        using var client = factory.Client(WriteToken());
        using var reader = factory.Client(ReadToken());
        var matchId = await CreateMatchAsync(client);
        var questionId = await CurrentQuestionAsync(reader, matchId);

        // Act
        using var response = await GameplayHttp.SendAsync(
            client,
            HttpMethod.Post,
            $"/matches/{matchId}/answers",
            new { questionId, answer = "A" });
        using var json = await GameplayHttp.JsonAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("correct").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("playerAScore").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("roundComplete").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("finished").GetBoolean().Should().BeFalse();
    }

    private static async Task<string> CreateMatchAsync(HttpClient client)
    {
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", CreateBody());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var json = await GameplayHttp.JsonAsync(response);
        return json.RootElement.GetProperty("matchId").GetString()!;
    }

    private static async Task<Guid> CurrentQuestionAsync(HttpClient client, string matchId)
    {
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Get, $"/matches/{matchId}/next-question");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await GameplayHttp.JsonAsync(response);
        return json.RootElement.GetProperty("questionId").GetGuid();
    }

    private static async Task AnswerAsync(HttpClient client, string matchId, Guid questionId)
    {
        using var response = await GameplayHttp.SendAsync(
            client,
            HttpMethod.Post,
            $"/matches/{matchId}/answers",
            new { questionId, answer = "A" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
