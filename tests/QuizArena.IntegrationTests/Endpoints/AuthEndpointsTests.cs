using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using QuizArena.Domain.Contracts;
using QuizArena.IntegrationTests.Infrastructure;

namespace QuizArena.IntegrationTests.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuthEndpointsTests(QuizArenaWebApplicationFactory factory)
{
    [Fact]
    public async Task Register_login_and_get_user_work_without_authentication()
    {
        using var client = factory.CreateClient();
        var username = $"user-{Guid.NewGuid():N}";
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, "password"));
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await register.Content.ReadFromJsonAsync<UserDto>();
        user.Should().NotBeNull();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "password"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/users/{user!.UserId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Duplicate_registration_and_invalid_login_return_conflicts()
    {
        using var client = factory.CreateClient();
        var request = new RegisterRequest($"duplicate-{Guid.NewGuid():N}", "password");
        await client.PostAsJsonAsync("/api/auth/register", request);
        (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("missing", "password"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
