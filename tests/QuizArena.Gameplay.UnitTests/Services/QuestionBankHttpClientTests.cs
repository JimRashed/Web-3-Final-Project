using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Domain.Exceptions;
using QuizArena.Gameplay.UnitTests.Outbox;
using Xunit;

namespace QuizArena.Gameplay.UnitTests.Services;

public sealed class QuestionBankHttpClientTests
{
    private static string QuestionSet(int count, string correctAnswer = "A")
    {
        var questions = Enumerable.Range(0, count).Select(index => $$"""
            {
              "id": "0000000{{index}}-1111-2222-3333-444444444444",
              "prompt": "Question {{index}}?",
              "category": "general",
              "options": ["A", "B", "C", "D"],
              "correctAnswer": "{{correctAnswer}}"
            }
            """);

        return $"[{string.Join(",", questions)}]";
    }

    private static QuestionBankHttpClient Sut(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        out StubHttpMessageHandler handler,
        out StubTokenSource tokens,
        string questionBankUrl = "http://questionbank.test")
    {
        handler = new StubHttpMessageHandler(respond);
        tokens = new StubTokenSource();
        return new QuestionBankHttpClient(
            new HttpClient(handler),
            tokens,
            Options.Create(new GameplayServiceOptions { QuestionBank = questionBankUrl }),
            NullLogger<QuestionBankHttpClient>.Instance);
    }

    [Fact]
    public async Task SnapshotAsync_WhenTheServiceReturnsTheExactCount_MaterializesSnapshots()
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, QuestionSet(5)), out var handler, out var tokens);

        // Act
        var snapshots = await sut.SnapshotAsync(5, CancellationToken.None);

        // Assert
        snapshots.Should().HaveCount(5);
        snapshots.Select(snapshot => snapshot.QuestionId).Should().OnlyHaveUniqueItems();
        snapshots[0].Text.Should().Be("Question 0?");
        snapshots[0].Options.Should().Equal("A", "B", "C", "D");
        snapshots[0].CorrectAnswer.Should().Be("A");
        handler.Requests.Single().RequestUri!.ToString().Should().Be("http://questionbank.test/internal/question-sets");
        tokens.Scopes.Should().Equal(GameplayServiceOptions.Scopes.QuestionsPlay);
        handler.Bodies.Single().Should().Be("{\"count\":5}");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public async Task SnapshotAsync_WhenTheServiceReturnsTheWrongCount_FailsClosedWith503(int returned)
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, QuestionSet(returned)), out _, out _);

        // Act
        var act = () => sut.SnapshotAsync(5, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task SnapshotAsync_WhenTheCatalogIsTooSmall_FailsClosedWith503()
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.Conflict), out _, out _);

        // Act
        var act = () => sut.SnapshotAsync(5, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task SnapshotAsync_WhenASnapshotIsMalformed_FailsClosedWith503()
    {
        // Arrange — the answer is not one of the options, so the snapshot cannot be graded.
        var malformed = QuestionSet(5, "Z");
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, malformed), out _, out _);

        // Act
        var act = () => sut.SnapshotAsync(5, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task SnapshotAsync_WhenTheAddressIsNotConfigured_FailsClosedWith503()
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, QuestionSet(5)), out var handler, out _, questionBankUrl: string.Empty);

        // Act
        var act = () => sut.SnapshotAsync(5, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(503);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SnapshotAsync_WithANonPositiveCount_Returns400()
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, "[]"), out _, out _);

        // Act
        var act = () => sut.SnapshotAsync(0, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GameplayRequestException>()).Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SnapshotAsync_DoesNotLeakTheCorrectAnswerIntoAnExceptionMessage()
    {
        // Arrange
        var sut = Sut(_ => StubHttpMessageHandler.Json(HttpStatusCode.InternalServerError), out _, out _);

        // Act
        var act = () => sut.SnapshotAsync(5, CancellationToken.None);

        // Assert
        var exception = (await act.Should().ThrowAsync<GameplayRequestException>()).Which;
        exception.Message.Should().NotContain("Answer");
    }
}
