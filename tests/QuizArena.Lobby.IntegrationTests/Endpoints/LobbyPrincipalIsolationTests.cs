using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Lobby.Api.Contracts;
using QuizArena.Lobby.Api.Security;
using QuizArena.Lobby.Domain.Entities;
using QuizArena.Lobby.Domain.Enums;
using QuizArena.Lobby.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Endpoints;

public sealed class LobbyPrincipalIsolationTests(LobbyApiFixture fixture) : IClassFixture<LobbyApiFixture>
{
    [Theory]
    [InlineData("missing")]
    [InlineData("blank")]
    [InlineData("whitespace")]
    public async Task Create_MissingOrBlankSignedSubject_DeniesWithoutWriting(string scenario)
    {
        // Arrange
        await using var before = fixture.Database.CreateContext();
        var count = await before.ActiveLobbies.AsNoTracking().CountAsync();
        var token = TestTokens.Create(ActorKind.User, LobbyPolicies.LobbiesWrite, claims =>
        {
            if (scenario == "missing")
            {
                claims.Remove("sub");
            }
            else
            {
                claims["sub"] = scenario == "blank" ? "" : "   ";
            }
        });

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, "/api/lobbies", token, "{}");

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 403);
        await using var reader = fixture.Database.CreateContext();
        (await reader.ActiveLobbies.AsNoTracking().CountAsync()).Should().Be(count);
    }

    [Fact]
    public async Task Join_OversizedSignedSubject_Returns400WithoutChangingStoredRoster()
    {
        // Arrange
        var lobby = await fixture.SeedAsync();
        var token = LobbyApiFixture.Token(subject: new string('x', LobbyPlayer.MaxSubjectLength + 1));

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, $"/api/lobbies/{lobby.JoinCode}/join", token, "{}");

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 400);
        await using var reader = fixture.Database.CreateContext();
        var stored = await reader.ActiveLobbies.AsNoTracking().Include(value => value.Players).SingleAsync(value => value.Id == lobby.Id);
        stored.Status.Should().Be(LobbyStatus.Open);
        stored.Players.Select(player => player.PlayerId).Should().Equal("host");
    }

    [Fact]
    public async Task Preview_CaseVariantOfHost_RemainsNonmemberWithoutLeakingRoster()
    {
        // Arrange
        var lobby = await fixture.SeedAsync(full: true);

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Get, $"/api/lobbies/{lobby.JoinCode}", LobbyApiFixture.Token(LobbyPolicies.LobbiesRead, "Host"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<LobbyResponse>())!;
        preview.IsHost.Should().BeFalse();
        preview.IsMember.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("hostUserId").And.NotContain("playerIds");
    }

    [Fact]
    public async Task Lock_CaseVariantWithSpoofedHostMetadata_RemainsForbiddenAndOpen()
    {
        // Arrange
        var lobby = await fixture.SeedAsync(full: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/lobbies/{lobby.JoinCode}/lock?hostId=host");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LobbyApiFixture.Token(subject: "Host"));
        request.Headers.Add("X-Host-Id", "host");

        // Act
        using var response = await fixture.Client.SendAsync(request);

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 403);
        await using var reader = fixture.Database.CreateContext();
        var stored = await reader.ActiveLobbies.AsNoTracking().Include(value => value.Players).SingleAsync(value => value.Id == lobby.Id);
        stored.Status.Should().Be(LobbyStatus.Open);
        stored.HostId.Should().Be("host");
        stored.Players.Select(player => player.PlayerId).Should().BeEquivalentTo("host", "guest");
    }

    [Theory]
    [InlineData("other-service")]
    [InlineData("browser")]
    public async Task InternalLookup_UntrustedClientOrActor_CannotReadPrivateRoster(string scenario)
    {
        // Arrange
        var lobby = await fixture.SeedAsync(full: true);
        var token = scenario == "other-service"
            ? TestTokens.Create(ActorKind.Machine, LobbyPolicies.LobbiesVerify, claims => claims["azp"] = "quizarena-notification")
            : TestTokens.Create(ActorKind.User, LobbyPolicies.LobbiesVerify);

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Get, $"/internal/lobbies/{lobby.JoinCode}", token);

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 403);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("hostUserId").And.NotContain("playerIds").And.NotContain("guest");
        await using var reader = fixture.Database.CreateContext();
        var stored = await reader.ActiveLobbies.AsNoTracking().Include(value => value.Players).SingleAsync(value => value.Id == lobby.Id);
        stored.Status.Should().Be(LobbyStatus.Open);
        stored.Players.Select(player => player.PlayerId).Should().BeEquivalentTo("host", "guest");
    }
}
