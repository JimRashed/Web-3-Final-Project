using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Lobby.Api.Contracts;
using QuizArena.Lobby.Api.Security;
using QuizArena.Lobby.Domain.Enums;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Endpoints;

public sealed class LobbyLifecycleTests(LobbyApiFixture fixture) : IClassFixture<LobbyApiFixture>
{
    [Fact]
    public async Task Lifecycle_TwoTrustedSubjects_PersistsIdempotentJoinAndLockedState()
    {
        // Arrange
        using var created = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, "/api/lobbies", LobbyApiFixture.Token(), "{}");
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var initial = (await created.Content.ReadFromJsonAsync<LobbyResponse>())!;
        created.Headers.Location!.IsAbsoluteUri.Should().BeFalse();
        created.Headers.Location.OriginalString.Should().Be($"/api/lobbies/{initial.JoinCode}");
        using var preview = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Get, created.Headers.Location.OriginalString, LobbyApiFixture.Token(LobbyPolicies.LobbiesRead));
        (await preview.Content.ReadFromJsonAsync<LobbyResponse>()).Should().Be(initial);
        using var outsider = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Get, created.Headers.Location.OriginalString, LobbyApiFixture.Token(LobbyPolicies.LobbiesRead, "outsider"));
        var outsiderView = (await outsider.Content.ReadFromJsonAsync<LobbyResponse>())!;
        outsiderView.IsHost.Should().BeFalse();
        outsiderView.IsMember.Should().BeFalse();
        using var join = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, $"/api/lobbies/{initial.JoinCode}/join", LobbyApiFixture.Token(subject: "guest"), "{}");
        join.StatusCode.Should().Be(HttpStatusCode.OK);
        var joined = (await join.Content.ReadFromJsonAsync<LobbyResponse>())!;
        joined.MemberCount.Should().Be(2);
        joined.IsHost.Should().BeFalse();
        joined.IsMember.Should().BeTrue();
        using var repeatedJoin = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, $"/api/lobbies/{initial.JoinCode}/join", LobbyApiFixture.Token(subject: "guest"), "{}");
        (await repeatedJoin.Content.ReadFromJsonAsync<LobbyResponse>()).Should().Be(joined);
        using var third = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, $"/api/lobbies/{initial.JoinCode}/join", LobbyApiFixture.Token(subject: "third"), "{}");
        await LobbyApiFixture.AssertProblemAsync(third, 409);

        // Act
        using var locked = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, $"/api/lobbies/{initial.JoinCode}/lock", LobbyApiFixture.Token());

        // Assert
        locked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await locked.Content.ReadFromJsonAsync<LobbyResponse>())!.Status.Should().Be("Locked");
        using var repeatedLock = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, $"/api/lobbies/{initial.JoinCode}/lock", LobbyApiFixture.Token(), "{}");
        repeatedLock.StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var subject in new[] { "guest", "late" })
        {
            using var rejected = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, $"/api/lobbies/{initial.JoinCode}/join", LobbyApiFixture.Token(subject: subject), "{}");
            await LobbyApiFixture.AssertProblemAsync(rejected, 409);
        }

        using var machine = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Get, $"/internal/lobbies/{initial.JoinCode}", LobbyApiFixture.Token(LobbyPolicies.LobbiesVerify, "machine", ActorKind.Machine));
        machine.StatusCode.Should().Be(HttpStatusCode.OK);
        using var machineJson = System.Text.Json.JsonDocument.Parse(await machine.Content.ReadAsStringAsync());
        machineJson.RootElement.GetProperty("hostUserId").GetString().Should().Be("host");
        await using var reader = fixture.Database.CreateContext();
        var persisted = await reader.ActiveLobbies.AsNoTracking().Include(lobby => lobby.Players).SingleAsync(lobby => lobby.Id == initial.LobbyId);
        persisted.Status.Should().Be(LobbyStatus.Locked);
        persisted.Players.Select(player => player.PlayerId).Should().BeEquivalentTo("host", "guest");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lock_Nonhost_IsForbiddenForOpenAndLocked(bool locked)
    {
        // Arrange
        var lobby = await fixture.SeedAsync(full: true);
        if (locked)
        {
            await fixture.Database.Repository().SaveAsync(lobby.JoinCode, value => value.Lock("host"));
        }

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, $"/api/lobbies/{lobby.JoinCode}/lock", LobbyApiFixture.Token(subject: "guest"));

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 403);
    }

    [Fact]
    public async Task Lock_HostWithOneMember_Conflicts()
    {
        // Arrange
        var lobby = await fixture.SeedAsync();

        // Act
        using var response = await LobbyApiFixture.SendAsync(fixture.Client, HttpMethod.Post, $"/api/lobbies/{lobby.JoinCode}/lock", LobbyApiFixture.Token());

        // Assert
        await LobbyApiFixture.AssertProblemAsync(response, 409);
    }
}
