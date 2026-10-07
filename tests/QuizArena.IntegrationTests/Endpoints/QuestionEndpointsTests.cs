using System.Net;
using FluentAssertions;
using QuizArena.IntegrationTests.Infrastructure;

namespace QuizArena.IntegrationTests.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class QuestionEndpointsTests : IntegrationTestBase
{
    public QuestionEndpointsTests(QuizArenaWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Generate_returns_questions_and_rejects_invalid_count()
    {
        using var client = Factory.CreateClient();
        (await client.GetAsync("/api/questions/generate?count=1")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/questions/generate?count=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
