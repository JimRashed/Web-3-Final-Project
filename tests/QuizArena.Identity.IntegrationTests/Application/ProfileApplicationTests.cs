using System.Text.Json;
using FluentAssertions;
using Moq;
using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Api.Services;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Exceptions;
using QuizArena.Identity.Domain.Repositories;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Application;

/// <summary>
/// Unit tests for the Identity application layer with a mocked repository. They live here because the
/// architecture rules let only this project reference the Api assembly.
/// </summary>
public sealed class ProfileApplicationTests
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly AuthenticatedActor Player = new(Issuer, "player-a", ActorKind.User);

    private readonly Mock<IPlayerProfileRepository> _profiles = new(MockBehavior.Strict);
    private readonly ProfileApplication _application;

    public ProfileApplicationTests()
    {
        _application = new ProfileApplication(_profiles.Object, new FixedClock(Now));
    }

    [Fact]
    public async Task GetMeAsync_WithoutProfile_ReturnsNull()
    {
        // Arrange
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);

        // Act
        var result = await _application.GetMeAsync(Player, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMeAsync_WithProfile_MapsAllFieldsFromStoredProfile()
    {
        // Arrange
        var stored = PlayerProfile.Create(Issuer, "player-a", "Alex", Now);
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        // Act
        var result = await _application.GetMeAsync(Player, CancellationToken.None);

        // Assert
        result.Should().Be(new ProfileResponse(stored.Id, Issuer, "player-a", "Alex"));
    }

    [Fact]
    public async Task GetMeAsync_UsesOnlyTheTokenIdentityAndForwardsCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", cancellation.Token)).ReturnsAsync((PlayerProfile?)null);

        // Act
        await _application.GetMeAsync(Player, cancellation.Token);

        // Assert
        _profiles.Verify(r => r.FindByIdentityAsync(Issuer, "player-a", cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task UpsertMeAsync_WithExistingProfile_RenamesAndReturnsIt()
    {
        // Arrange
        var renamed = PlayerProfile.Create(Issuer, "player-a", "Sam", Now.AddDays(-1));
        _profiles.Setup(r => r.RenameAsync(Issuer, "player-a", " Sam ", Now, It.IsAny<CancellationToken>())).ReturnsAsync(renamed);

        // Act
        var result = await _application.UpsertMeAsync(Player, new UpdateProfileRequest(" Sam "), CancellationToken.None);

        // Assert
        result.DisplayName.Should().Be("Sam");
        result.Id.Should().Be(renamed.Id);
        _profiles.Verify(r => r.AddIfAbsentAsync(It.IsAny<PlayerProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpsertMeAsync_WithoutProfile_CreatesProfileForTokenIdentity()
    {
        // Arrange
        PlayerProfile? added = null;
        _profiles.Setup(r => r.RenameAsync(Issuer, "player-a", "Alex", Now, It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);
        _profiles.Setup(r => r.AddIfAbsentAsync(It.IsAny<PlayerProfile>(), It.IsAny<CancellationToken>()))
            .Callback<PlayerProfile, CancellationToken>((profile, _) => added = profile)
            .ReturnsAsync((PlayerProfile profile, CancellationToken _) => profile);

        // Act
        var result = await _application.UpsertMeAsync(Player, new UpdateProfileRequest("Alex"), CancellationToken.None);

        // Assert
        added.Should().NotBeNull();
        added!.Issuer.Should().Be(Issuer);
        added.Subject.Should().Be("player-a");
        added.CreatedAt.Should().Be(Now);
        result.Should().Be(new ProfileResponse(added.Id, Issuer, "player-a", "Alex"));
    }

    [Fact]
    public async Task UpsertMeAsync_WhenConcurrentFirstSaveWins_AppliesRequestedNameToStoredProfile()
    {
        // Arrange
        var winner = PlayerProfile.Create(Issuer, "player-a", "Other tab", Now);
        var winnerRenamed = PlayerProfile.Rehydrate(winner.Id, Issuer, "player-a", "Alex", Now, Now);
        _profiles.SetupSequence(r => r.RenameAsync(Issuer, "player-a", "Alex", Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerProfile?)null)
            .ReturnsAsync(winnerRenamed);
        _profiles.Setup(r => r.AddIfAbsentAsync(It.IsAny<PlayerProfile>(), It.IsAny<CancellationToken>())).ReturnsAsync(winner);

        // Act
        var result = await _application.UpsertMeAsync(Player, new UpdateProfileRequest("Alex"), CancellationToken.None);

        // Assert
        result.Id.Should().Be(winner.Id);
        result.DisplayName.Should().Be("Alex");
        _profiles.Verify(r => r.RenameAsync(Issuer, "player-a", "Alex", Now, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task UpsertMeAsync_NewProfileWithInvalidName_ThrowsBeforeWriting(string? displayName)
    {
        // Arrange
        _profiles.Setup(r => r.RenameAsync(Issuer, "player-a", It.IsAny<string>(), Now, It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);

        // Act
        var act = () => _application.UpsertMeAsync(Player, new UpdateProfileRequest(displayName), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidProfileException>()).Which.Field.Should().Be("displayName");
        _profiles.Verify(r => r.AddIfAbsentAsync(It.IsAny<PlayerProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Operations_WithMachineActor_AreRefused()
    {
        // Arrange
        var machine = new AuthenticatedActor(Issuer, "service-account-quizarena-gameplay", ActorKind.Machine);

        // Act
        var get = () => _application.GetMeAsync(machine, CancellationToken.None);
        var put = () => _application.UpsertMeAsync(machine, new UpdateProfileRequest("Bot"), CancellationToken.None);

        // Assert
        await get.Should().ThrowAsync<InvalidOperationException>();
        await put.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void ProfileResponse_SerializedAsWebJson_MatchesContractProfileSchema()
    {
        // Arrange
        var response = new ProfileResponse(Guid.NewGuid(), Issuer, "player-a", "Alex");
        var contract = ContractSchemaProperties("Profile");

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        // Assert
        json.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(contract);
    }

    [Fact]
    public void UpdateProfileRequest_HasExactlyTheContractFields()
    {
        // Arrange
        var contract = ContractSchemaProperties("UpdateProfile");

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new UpdateProfileRequest("Alex"), new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        // Assert
        json.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(contract);
    }

    private static IReadOnlyList<string> ContractSchemaProperties(string schema)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory!.FullName, "contracts", "openapi", "identity.yaml");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schema)
            .GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
