using System.Text.Json;
using FluentAssertions;
using Moq;
using QuizArena.Statistics.Api.Contracts;
using QuizArena.Statistics.Api.Security;
using QuizArena.Statistics.Api.Services;
using QuizArena.Statistics.Domain.Entities;
using QuizArena.Statistics.Domain.Enums;
using QuizArena.Statistics.Domain.Exceptions;
using QuizArena.Statistics.Domain.Repositories;
using Xunit;

namespace QuizArena.Statistics.IntegrationTests.Application;

/// <summary>
/// Unit tests for the Statistics application layer with a mocked repository. They live here because the
/// architecture rules let only this project reference the Api assembly.
/// </summary>
public sealed class StatisticsApplicationTests
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private static readonly DateTime PlayedAt = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly AuthenticatedActor Player = new(Issuer, "player-a", ActorKind.User);
    private static readonly AuthenticatedActor Gameplay = new(Issuer, "service-account-quizarena-gameplay", ActorKind.Machine);

    private readonly Mock<IMatchResultRepository> _results = new(MockBehavior.Strict);
    private readonly StatisticsApplication _application;

    public StatisticsApplicationTests()
    {
        _application = new StatisticsApplication(_results.Object);
    }

    private static RecordResultRequest ValidRequest() =>
        new("26c94137-d660-4397-a731-050c2bb07001", "player-a", 3, true, new DateTimeOffset(PlayedAt));

    [Fact]
    public async Task RecordResultAsync_FromMachine_RecordsWithTokenIssuerAndUtcTime()
    {
        // Arrange
        MatchResult? recorded = null;
        _results.Setup(r => r.RecordAsync(It.IsAny<MatchResult>(), It.IsAny<CancellationToken>()))
            .Callback<MatchResult, CancellationToken>((result, _) => recorded = result)
            .ReturnsAsync(ResultReplayDecision.New);
        var request = ValidRequest() with { PlayedAt = new DateTimeOffset(2026, 11, 4, 14, 0, 0, TimeSpan.FromHours(2)) };

        // Act
        var decision = await _application.RecordResultAsync(Gameplay, request, CancellationToken.None);

        // Assert
        decision.Should().Be(ResultReplayDecision.New);
        recorded!.Issuer.Should().Be(Issuer);
        recorded.UserId.Should().Be("player-a");
        recorded.Score.Should().Be(3);
        recorded.PlayedAt.Should().Be(PlayedAt);
        recorded.PlayedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task RecordResultAsync_IdenticalRetry_ReturnsReplayDecision()
    {
        // Arrange
        _results.Setup(r => r.RecordAsync(It.IsAny<MatchResult>(), It.IsAny<CancellationToken>())).ReturnsAsync(ResultReplayDecision.IdempotentReplay);

        // Act
        var decision = await _application.RecordResultAsync(Gameplay, ValidRequest(), CancellationToken.None);

        // Assert
        decision.Should().Be(ResultReplayDecision.IdempotentReplay);
    }

    [Theory]
    [InlineData("matchId")]
    [InlineData("userId")]
    [InlineData("score")]
    [InlineData("won")]
    [InlineData("playedAt")]
    public async Task RecordResultAsync_WithMissingField_ThrowsFieldErrorWithoutWriting(string field)
    {
        // Arrange
        var request = field switch
        {
            "matchId" => ValidRequest() with { MatchId = null },
            "userId" => ValidRequest() with { UserId = null },
            "score" => ValidRequest() with { Score = null },
            "won" => ValidRequest() with { Won = null },
            _ => ValidRequest() with { PlayedAt = null },
        };

        // Act
        var act = () => _application.RecordResultAsync(Gameplay, request, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidMatchResultException>()).Which.Field.Should().Be(field);
        _results.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RecordResultAsync_WithScoreAboveFive_ThrowsWithoutWriting()
    {
        // Act
        var act = () => _application.RecordResultAsync(Gameplay, ValidRequest() with { Score = 6 }, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidMatchResultException>()).Which.Field.Should().Be("score");
        _results.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RecordResultAsync_FromPlayer_IsRefused()
    {
        // Act
        var act = () => _application.RecordResultAsync(Player, ValidRequest(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetStatsAsync_ForSelf_ReturnsTotalsFromTokenIdentity()
    {
        // Arrange
        _results.Setup(r => r.GetTotalsAsync(Issuer, "player-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerTotals("player-a", 3, 1, 8));

        // Act
        var stats = await _application.GetStatsAsync(Player, "player-a", CancellationToken.None);

        // Assert
        stats.Should().Be(new StatsResponse("player-a", 3, 1, 8));
    }

    [Theory]
    [InlineData("player-b")]
    [InlineData("Player-A")]
    public async Task GetStatsAsync_ForAnotherPlayer_ThrowsForbiddenBeforeLookup(string userId)
    {
        // Act
        var act = () => _application.GetStatsAsync(Player, userId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _results.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetHistoryAsync_ForSelf_MapsItemsInRepositoryOrder()
    {
        // Arrange
        var newer = MatchResult.Record(Issuer, "m2", "player-a", 4, true, PlayedAt.AddHours(1));
        var older = MatchResult.Record(Issuer, "m1", "player-a", 1, false, PlayedAt);
        _results.Setup(r => r.GetHistoryAsync(Issuer, "player-a", 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([newer, older]);

        // Act
        var history = await _application.GetHistoryAsync(Player, "player-a", 2, 10, CancellationToken.None);

        // Assert
        history.Should().Equal(
            new HistoryItemResponse("m2", 4, true, PlayedAt.AddHours(1)),
            new HistoryItemResponse("m1", 1, false, PlayedAt));
    }

    [Fact]
    public async Task GetHistoryAsync_WithInvalidPagingForAnotherPlayer_Returns400Before403()
    {
        // Act
        var act = () => _application.GetHistoryAsync(Player, "player-b", 0, 20, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidMatchResultException>()).Which.Field.Should().Be("page");
        _results.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetHistoryAsync_ForAnotherPlayer_ThrowsForbidden()
    {
        // Act
        var act = () => _application.GetHistoryAsync(Player, "player-b", 1, 20, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _results.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetLeaderboardAsync_MapsEntriesInRankOrder()
    {
        // Arrange
        _results.Setup(r => r.GetLeaderboardAsync(Issuer, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LeaderboardEntry(1, "Zed", 5, 1, 1), new LeaderboardEntry(2, "alice", 5, 1, 2)]);

        // Act
        var board = await _application.GetLeaderboardAsync(Player, 2, CancellationToken.None);

        // Assert
        board.Should().Equal(new LeaderResponse("Zed", 5, 1), new LeaderResponse("alice", 5, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetLeaderboardAsync_WithTopOutOfRange_ThrowsWithoutQuerying(int top)
    {
        // Act
        var act = () => _application.GetLeaderboardAsync(Player, top, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidMatchResultException>()).Which.Field.Should().Be("top");
        _results.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Queries_ForwardCancellationToken()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        _results.Setup(r => r.GetTotalsAsync(Issuer, "player-a", cancellation.Token)).ReturnsAsync(new PlayerTotals("player-a", 0, 0, 0));

        // Act
        await _application.GetStatsAsync(Player, "player-a", cancellation.Token);

        // Assert
        _results.Verify(r => r.GetTotalsAsync(Issuer, "player-a", cancellation.Token), Times.Once);
    }

    [Theory]
    [InlineData("RecordResult")]
    [InlineData("Stats")]
    [InlineData("History")]
    [InlineData("Leader")]
    public void Dtos_SerializedAsWebJson_MatchContractSchemas(string schema)
    {
        // Arrange
        object dto = schema switch
        {
            "RecordResult" => ValidRequest(),
            "Stats" => new StatsResponse("player-a", 1, 1, 3),
            "History" => new HistoryItemResponse("m1", 3, true, PlayedAt),
            _ => new LeaderResponse("player-a", 3, 1),
        };

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, dto.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        // Assert
        json.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(ContractSchemaProperties(schema));
    }

    private static IReadOnlyList<string> ContractSchemaProperties(string schema)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory!.FullName, "contracts", "openapi", "statistics.yaml");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schema)
            .GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();
    }
}
