using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace QuizArena.Statistics.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<QuizArena.Statistics.Api.Program>>
{
    private readonly WebApplicationFactory<QuizArena.Statistics.Api.Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<QuizArena.Statistics.Api.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealthAsync_WithoutAuthenticationOrDependencies_ReturnsSafeLiveness()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.RootElement.EnumerateObject().Select(property => property.Name).Should().ContainSingle().Which.Should().Be("status");
        body.RootElement.GetProperty("status").GetString().Should().Be("ok");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
