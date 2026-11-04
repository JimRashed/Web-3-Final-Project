using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using QuizArena.Lobby.Api.Controllers;
using QuizArena.Lobby.Api.Security;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Endpoints;

public sealed class LobbyContractTests(LobbyApiFixture fixture) : IClassFixture<LobbyApiFixture>
{
    [Fact]
    public async Task PublicPreview_Nonmember_ContainsExactlyCommittedSafeFields()
    {
        // Arrange
        var lobby = await fixture.SeedAsync();

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Get, $"/api/lobbies/{lobby.JoinCode}", LobbyApiFixture.Token(LobbyPolicies.LobbiesRead, "outsider"));

        // Assert
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using var contract = ReadContract();
        var fields = contract.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("Lobby").GetProperty("properties").EnumerateObject().Select(property => property.Name);
        json.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(fields);
        json.RootElement.GetProperty("status").ValueKind.Should().Be(JsonValueKind.String);
        json.RootElement.GetProperty("isHost").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("isMember").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InternalProvision_ValidOpenOrLockedState_ContainsExactCommittedFields(bool full, bool locked)
    {
        // Arrange
        var lobby = await fixture.SeedAsync(full);
        if (locked)
        {
            await fixture.Database.Repository().SaveAsync(lobby.JoinCode, value => value.Lock("host"));
        }

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Get, $"/internal/lobbies/{lobby.JoinCode}", LobbyApiFixture.Token(LobbyPolicies.LobbiesVerify, "machine", ActorKind.Machine));

        // Assert
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using var contract = ReadContract();
        var fields = contract.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("Provision").GetProperty("properties").EnumerateObject().Select(property => property.Name);
        json.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(fields);
        json.RootElement.GetProperty("hostUserId").GetString().Should().Be("host");
        json.RootElement.GetProperty("playerIds").GetArrayLength().Should().Be(full ? 2 : 1);
        json.RootElement.GetProperty("status").GetString().Should().Be(locked ? "Locked" : "Open");
    }

    [Theory]
    [InlineData("/api/lobbies", "post", "Create", "lobbies.write", "user", 201)]
    [InlineData("/api/lobbies/{joinCode}", "get", "Preview", "lobbies.read", "user", 200)]
    [InlineData("/api/lobbies/{joinCode}/join", "post", "Join", "lobbies.write", "user", 200)]
    [InlineData("/api/lobbies/{joinCode}/lock", "post", "Lock", "lobbies.write", "user", 200)]
    [InlineData("/internal/lobbies/{joinCode}", "get", "Verify", "lobbies.verify", "machine", 200)]
    public void Routes_ComparedWithCommittedOpenApi_AgreeOnMethodPolicyActorAndBodies(string path, string verb, string action, string policy, string actor, int success)
    {
        // Arrange
        using var contract = ReadContract();
        var operation = contract.RootElement.GetProperty("paths").GetProperty(path).GetProperty(verb);
        var controller = action == "Verify" ? typeof(InternalLobbiesController) : typeof(LobbiesController);
        var method = controller.GetMethod(action)!;

        // Act
        var route = method.GetCustomAttribute<HttpMethodAttribute>()!;

        // Assert
        route.HttpMethods.Should().ContainSingle().Which.Should().Be(verb.ToUpperInvariant());
        var combined = "/" + controller.GetCustomAttribute<RouteAttribute>()!.Template + (route.Template is null ? "" : "/" + route.Template);
        combined.Should().Be(path);
        method.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(policy);
        operation.GetProperty("x-policy").GetString().Should().Be(policy);
        operation.GetProperty("x-actor").GetString().Should().Be(actor);
        operation.GetProperty("security")[0].GetProperty("oauth2").EnumerateArray().Select(value => value.GetString()).Should().ContainSingle().Which.Should().Be(policy);
        operation.GetProperty("responses").TryGetProperty(success.ToString(), out _).Should().BeTrue();
        if (action is "Create" or "Join")
        {
            operation.GetProperty("requestBody").GetProperty("required").GetBoolean().Should().BeTrue();
        }
        else if (action == "Lock")
        {
            operation.TryGetProperty("requestBody", out _).Should().BeFalse();
        }

        if (action == "Create")
        {
            operation.GetProperty("responses").GetProperty("201").GetProperty("headers").TryGetProperty("Location", out _).Should().BeTrue();
        }
    }

    private static JsonDocument ReadContract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
        {
            directory = directory.Parent;
        }

        return JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "contracts", "openapi", "lobby.yaml")));
    }
}
