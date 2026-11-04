using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;

namespace QuizArena.QuestionBank.IntegrationTests.Endpoints;

public sealed class QuestionInputTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("51")]
    [InlineData("-1")]
    [InlineData("SENTINEL")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("")]
    public async Task Generate_InvalidCount_ReturnsSafeBadRequestWithoutCallingService(string count)
    {
        // Arrange
        var service = new CatalogServiceDouble();
        using var fixture = new QuestionBankApiFixture(service);

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, false, QuestionBankApiFixture.Token(), $"/api/questions/generate?count={count}");

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, HttpStatusCode.BadRequest);
        service.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{\"count\":null}")]
    [InlineData("{\"count\":4}")]
    [InlineData("{\"count\":6}")]
    [InlineData("{\"count\":5.5}")]
    [InlineData("{\"count\":\"SENTINEL\"}")]
    [InlineData("{\"count\":5,\"userId\":\"SENTINEL\"}")]
    public async Task Create_InvalidBody_ReturnsSafeBadRequestWithoutCallingService(string body)
    {
        // Arrange
        var service = new CatalogServiceDouble();
        using var fixture = new QuestionBankApiFixture(service);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, true, QuestionBankApiFixture.Token(true), content: content);

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, HttpStatusCode.BadRequest);
        service.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Create_AbsentBodyWithoutContentType_ReturnsContractBadRequest()
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/question-sets");
        request.Headers.Authorization = new("Bearer", QuestionBankApiFixture.Token(true));

        // Act
        using var response = await fixture.Client.SendAsync(request);

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Route_InvalidInputWithoutPermission_AuthenticatesBeforeBinding(bool machine, bool authenticated)
    {
        // Arrange
        var service = new CatalogServiceDouble();
        using var fixture = new QuestionBankApiFixture(service);
        var token = authenticated ? QuestionBankApiFixture.Token(machine, "unrelated") : null;
        using var content = machine ? new StringContent("SENTINEL", Encoding.UTF8, "application/json") : null;

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, machine, token,
            machine ? null : "/api/questions/generate?count=SENTINEL", content);

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
        service.Calls.Should().Be(0);
    }
}
