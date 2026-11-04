using System.Net;
using System.Text.Json;
using FluentAssertions;
using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.IntegrationTests.Security;
using Xunit;

namespace QuizArena.QuestionBank.IntegrationTests.Endpoints;

public sealed class QuestionEndpointsTests
{
    [Theory]
    [InlineData(null, 10, 10)]
    [InlineData(1, 1, 1)]
    [InlineData(50, 50, 12)]
    public async Task Generate_ValidCount_ReturnsOnlyPublicFields(int? count, int forwarded, int expected)
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();
        var path = "/api/questions/generate" + (count is null ? "" : $"?count={count}");

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, false, QuestionBankApiFixture.Token(), path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fixture.Catalog.LastCount.Should().Be(forwarded);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetArrayLength().Should().Be(expected);
        foreach (var question in json.RootElement.EnumerateArray())
        {
            question.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "prompt", "category", "options");
            question.GetProperty("options").GetArrayLength().Should().Be(4);
        }
        json.RootElement.EnumerateArray().Select(question => question.GetProperty("id").GetGuid()).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Generate_SmallCatalog_ReturnsAvailableQuestions(int available)
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();
        fixture.Catalog.Questions = fixture.Catalog.Questions.Take(available).ToArray();

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, false, QuestionBankApiFixture.Token());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetArrayLength().Should().Be(available);
    }

    [Fact]
    public async Task Create_ValidMachineRequest_ReturnsFiveDistinctAnswerSnapshots()
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, true, QuestionBankApiFixture.Token(true));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fixture.Catalog.LastCount.Should().Be(5);
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        json.RootElement.GetArrayLength().Should().Be(5);
        foreach (var question in json.RootElement.EnumerateArray())
        {
            question.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "prompt", "category", "options", "correctAnswer");
            question.GetProperty("options").EnumerateArray().Select(option => option.GetString()).Should().Contain(question.GetProperty("correctAnswer").GetString());
        }
        json.RootElement.EnumerateArray().Select(question => question.GetProperty("id").GetGuid()).Should().OnlyHaveUniqueItems();
        fixture.Catalog.Questions = [];
        json.RootElement.GetArrayLength().Should().Be(5);
        text.Should().Contain("Question 1?");
    }

    [Fact]
    public async Task Create_InsufficientCatalog_ReturnsSafeConflict()
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();
        fixture.Catalog.Questions = fixture.Catalog.Questions.Take(4).ToArray();

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, true, QuestionBankApiFixture.Token(true));

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Route_SpoofedCallerInput_UsesValidatedActor(bool machine)
    {
        // Arrange
        var service = new CatalogServiceDouble();
        using var fixture = new QuestionBankApiFixture(service);
        var path = machine ? "/internal/question-sets?userId=spoofed-user" : "/api/questions/generate?userId=spoofed-user";

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, machine, QuestionBankApiFixture.Token(machine), path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        service.Actor.Should().Be(new AuthenticatedActor(TestTokens.Issuer, machine ? TestTokens.MachineSubject : TestTokens.UserSubject, machine ? ActorKind.Machine : ActorKind.User));
        service.Calls.Should().Be(1);
        service.Token.CanBeCanceled.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Route_ClientAborts_PropagatesCancellationToApplication(bool machine)
    {
        // Arrange
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new CatalogServiceDouble
        {
            OnCall = async token =>
            {
                entered.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, token); }
                finally { if (token.IsCancellationRequested) cancelled.TrySetResult(); }
            },
        };
        using var fixture = new QuestionBankApiFixture(service);
        using var cancellation = new CancellationTokenSource();

        // Act
        var pending = QuestionBankApiFixture.SendAsync(fixture.Client, machine, QuestionBankApiFixture.Token(machine), cancellationToken: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();

        // Assert
        await FluentActions.Awaiting(async () => await pending).Should().ThrowAsync<OperationCanceledException>();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        service.Token.IsCancellationRequested.Should().BeTrue();
    }
}
