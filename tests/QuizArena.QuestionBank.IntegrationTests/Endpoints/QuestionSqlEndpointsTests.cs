using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace QuizArena.QuestionBank.IntegrationTests.Endpoints;

public sealed class QuestionSqlEndpointsTests : IAsyncLifetime
{
    private readonly QuestionBankSqlApiFixture _fixture = new();
    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Theory]
    [InlineData("?count=1", 1)]
    [InlineData("", 5)]
    [InlineData("?count=50", 5)]
    public async Task Generate_PersistedCatalog_ReturnsDistinctAnswerFreeValuesWithoutMutation(string query, int expectedCount)
    {
        // Arrange
        var before = await _fixture.ReadCatalogAsync();

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(_fixture.Client, false, QuestionBankApiFixture.Token(),
            $"/api/questions/generate{query}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("correctAnswer");
        using var json = JsonDocument.Parse(body);
        AssertPersistedQuestions(json.RootElement, before, expectedCount, answers: false);
        (await _fixture.ReadCatalogAsync()).Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task CreateQuestionSet_GameplayPrincipal_ReturnsFivePersistedAnswerSnapshotsWithoutMutation()
    {
        // Arrange
        var before = await _fixture.ReadCatalogAsync();

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(_fixture.Client, true, QuestionBankApiFixture.Token(machine: true));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertPersistedQuestions(json.RootElement, before, 5, answers: true);
        (await _fixture.ReadCatalogAsync()).Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Generate_FourPersistedQuestions_ReturnsAvailableAnswerFreeCatalogWithoutMutation()
    {
        // Arrange
        await _fixture.RemoveOneQuestionAsync();
        var before = await _fixture.ReadCatalogAsync();
        before.Questions.Should().HaveCount(4);

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(_fixture.Client, false, QuestionBankApiFixture.Token(),
            "/api/questions/generate?count=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertPersistedQuestions(json.RootElement, before, 4, answers: false);
        (await _fixture.ReadCatalogAsync()).Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task CreateQuestionSet_FourPersistedQuestions_ReturnsSafeConflictWithoutMutation()
    {
        // Arrange
        await _fixture.RemoveOneQuestionAsync();
        var before = await _fixture.ReadCatalogAsync();
        before.Questions.Should().HaveCount(4);

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(_fixture.Client, true, QuestionBankApiFixture.Token(machine: true));

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, HttpStatusCode.Conflict);
        (await _fixture.ReadCatalogAsync()).Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    private static void AssertPersistedQuestions(JsonElement response, CatalogState catalog, int expectedCount, bool answers)
    {
        response.GetArrayLength().Should().Be(expectedCount);
        response.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().OnlyHaveUniqueItems();
        foreach (var item in response.EnumerateArray())
        {
            var fields = answers ? new[] { "id", "prompt", "category", "options", "correctAnswer" } : ["id", "prompt", "category", "options"];
            item.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(fields);
            var persisted = catalog.Questions.Single(question => question.Id == item.GetProperty("id").GetGuid());
            item.GetProperty("prompt").GetString().Should().Be(persisted.Prompt);
            item.GetProperty("category").GetString().Should().Be(catalog.Categories.Single(category => category.Id == persisted.CategoryId).Name);
            item.GetProperty("options").EnumerateArray().Select(option => option.GetString()).Should()
                .Equal(catalog.Options.Where(option => option.QuestionId == persisted.Id).Select(option => option.Value));
            if (answers) item.GetProperty("correctAnswer").GetString().Should().Be(persisted.CorrectAnswer);
        }
    }
}
