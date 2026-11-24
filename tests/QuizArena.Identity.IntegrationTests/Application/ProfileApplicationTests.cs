using System.Text.Json;
using FluentAssertions;
using Moq;
using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Api.Services;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Exceptions;
using QuizArena.Identity.Domain.Repositories;
using QuizArena.Identity.IntegrationTests.Endpoints;
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
    private readonly Mock<IProfilePictureStore> _pictures = new(MockBehavior.Strict);
    private readonly ProfileApplication _application;

    public ProfileApplicationTests()
    {
        _application = new ProfileApplication(_profiles.Object, _pictures.Object, new FixedClock(Now));
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
        _pictures.Setup(s => s.GetInfoAsync(stored.Id, It.IsAny<CancellationToken>())).ReturnsAsync((ProfilePictureInfo?)null);

        // Act
        var result = await _application.GetMeAsync(Player, CancellationToken.None);

        // Assert
        result.Should().Be(new ProfileResponse(stored.Id, Issuer, "player-a", "Alex", null));
    }

    [Fact]
    public async Task GetMeAsync_WithPicture_IncludesInfo()
    {
        // Arrange
        var stored = PlayerProfile.Create(Issuer, "player-a", "Alex", Now);
        using var cancellation = new CancellationTokenSource();
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", cancellation.Token)).ReturnsAsync(stored);
        _pictures.Setup(s => s.GetInfoAsync(stored.Id, cancellation.Token)).ReturnsAsync(new ProfilePictureInfo("image/webp", 3, Now));

        // Act
        var result = await _application.GetMeAsync(Player, cancellation.Token);

        // Assert
        result!.Picture.Should().Be(new ProfilePictureResponse("image/webp", Now, 3));
        _pictures.Verify(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("get-me")]
    [InlineData("upsert-rename")]
    [InlineData("upsert-race")]
    public async Task ProfileOperations_ReadPictureMetadataOnly_NeverPictureBytes(string operation)
    {
        // Arrange: #143 review: /me reads use GetInfoAsync; GetAsync (full bytes) has no setup, so the strict mock fails any call.
        var stored = PlayerProfile.Create(Issuer, "player-a", "Alex", Now.AddDays(-1));
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation == "upsert-race" ? null : stored);
        _profiles.Setup(r => r.RenameAsync(Issuer, "player-a", "Alex", Now, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _profiles.Setup(r => r.AddIfAbsentAsync(It.IsAny<PlayerProfile>(), It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _pictures.Setup(s => s.GetInfoAsync(stored.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new ProfilePictureInfo("image/png", 1, Now));

        // Act
        var result = operation == "get-me"
            ? await _application.GetMeAsync(Player, CancellationToken.None)
            : await _application.UpsertMeAsync(Player, new UpdateProfileRequest("Alex"), CancellationToken.None);

        // Assert
        result!.Picture.Should().Be(new ProfilePictureResponse("image/png", Now, 1));
        _pictures.Verify(s => s.GetInfoAsync(stored.Id, It.IsAny<CancellationToken>()), Times.Once);
        _pictures.Verify(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _pictures.VerifyNoOtherCalls();
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
        var existing = PlayerProfile.Create(Issuer, "player-a", "Alex", Now.AddDays(-1));
        var renamed = PlayerProfile.Rehydrate(existing.Id, Issuer, "player-a", "Sam", Now.AddDays(-1), Now);
        var sequence = new MockSequence();
        _profiles.InSequence(sequence).Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _pictures.InSequence(sequence).Setup(s => s.GetInfoAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new ProfilePictureInfo("image/png", 2, Now));
        _profiles.InSequence(sequence).Setup(r => r.RenameAsync(Issuer, "player-a", " Sam ", Now, It.IsAny<CancellationToken>())).ReturnsAsync(renamed);

        // Act
        var result = await _application.UpsertMeAsync(Player, new UpdateProfileRequest(" Sam "), CancellationToken.None);

        // Assert
        result.DisplayName.Should().Be("Sam");
        result.Id.Should().Be(renamed.Id);
        result.Picture.Should().Be(new ProfilePictureResponse("image/png", Now, 2));
        _profiles.Verify(r => r.AddIfAbsentAsync(It.IsAny<PlayerProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpsertMeAsync_WithoutProfile_CreatesProfileForTokenIdentity()
    {
        // Arrange
        PlayerProfile? added = null;
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);
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
        result.Should().Be(new ProfileResponse(added.Id, Issuer, "player-a", "Alex", null));
        _profiles.Verify(r => r.RenameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _pictures.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpsertMeAsync_WhenConcurrentFirstSaveWins_AppliesRequestedNameToStoredProfile()
    {
        // Arrange
        var winner = PlayerProfile.Create(Issuer, "player-a", "Other tab", Now);
        var winnerRenamed = PlayerProfile.Rehydrate(winner.Id, Issuer, "player-a", "Alex", Now, Now);
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);
        _profiles.Setup(r => r.RenameAsync(Issuer, "player-a", "Alex", Now, It.IsAny<CancellationToken>())).ReturnsAsync(winnerRenamed);
        _profiles.Setup(r => r.AddIfAbsentAsync(It.IsAny<PlayerProfile>(), It.IsAny<CancellationToken>())).ReturnsAsync(winner);
        _pictures.Setup(s => s.GetInfoAsync(winner.Id, It.IsAny<CancellationToken>())).ReturnsAsync((ProfilePictureInfo?)null);

        // Act
        var result = await _application.UpsertMeAsync(Player, new UpdateProfileRequest("Alex"), CancellationToken.None);

        // Assert
        result.Id.Should().Be(winner.Id);
        result.DisplayName.Should().Be("Alex");
        _profiles.Verify(r => r.RenameAsync(Issuer, "player-a", "Alex", Now, It.IsAny<CancellationToken>()), Times.Once);
        _pictures.Verify(s => s.GetInfoAsync(winner.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertMeAsync_WhenPictureMetadataReadFails_ThrowsBeforeRenaming()
    {
        // Arrange
        var existing = PlayerProfile.Create(Issuer, "player-a", "Alex", Now.AddDays(-1));
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _pictures.Setup(s => s.GetInfoAsync(existing.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());

        // Act
        var act = () => _application.UpsertMeAsync(Player, new UpdateProfileRequest("Sam"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        _profiles.Verify(r => r.RenameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _profiles.Verify(r => r.AddIfAbsentAsync(It.IsAny<PlayerProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task UpsertMeAsync_NewProfileWithInvalidName_ThrowsBeforeWriting(string? displayName)
    {
        // Arrange
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);

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
    public async Task ReplaceMyPictureAsync_UsesProfileIdFromExactIdentity()
    {
        // Arrange
        var stored = PlayerProfile.Create(Issuer, "player-a", "Alex", Now.AddDays(-1));
        var content = PictureSamples.Png;
        var info = new ProfilePictureInfo("image/png", 1, Now);
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _pictures.Setup(s => s.ReplaceAsync(stored.Id, "image/png", content, Now, It.IsAny<CancellationToken>())).ReturnsAsync(info);

        // Act
        var result = await _application.ReplaceMyPictureAsync(Player, "image/png", content, CancellationToken.None);

        // Assert
        result.Should().Be(new ProfileResponse(stored.Id, Issuer, "player-a", "Alex", new ProfilePictureResponse("image/png", Now, 1)));
        _pictures.Verify(s => s.ReplaceAsync(stored.Id, "image/png", content, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplaceMyPictureAsync_ForCaseVariantSubject_DoesNotReachOtherProfile()
    {
        // Arrange
        var other = PlayerProfile.Create(Issuer, "player-a", "Alex", Now.AddDays(-1));
        var variant = new AuthenticatedActor(Issuer, "Player-A", ActorKind.User);
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync(other);
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "Player-A", It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);

        // Act
        var act = () => _application.ReplaceMyPictureAsync(variant, "image/png", PictureSamples.Png, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<PlayerProfileNotFoundException>();
        _profiles.Verify(r => r.FindByIdentityAsync(Issuer, "Player-A", It.IsAny<CancellationToken>()), Times.Once);
        _pictures.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReplaceMyPictureAsync_ForSameSubjectFromOtherIssuer_DoesNotReachOtherProfile()
    {
        // Arrange
        const string otherIssuer = "https://other-idp.example/realms/quizarena";
        var other = PlayerProfile.Create(Issuer, "player-a", "Alex", Now.AddDays(-1));
        var foreign = new AuthenticatedActor(otherIssuer, "player-a", ActorKind.User);
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync(other);
        _profiles.Setup(r => r.FindByIdentityAsync(otherIssuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);

        // Act
        var act = () => _application.ReplaceMyPictureAsync(foreign, "image/png", PictureSamples.Png, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<PlayerProfileNotFoundException>();
        _profiles.Verify(r => r.FindByIdentityAsync(otherIssuer, "player-a", It.IsAny<CancellationToken>()), Times.Once);
        _pictures.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReplaceMyPictureAsync_WithoutProfile_ThrowsProfileNotFound()
    {
        // Arrange
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);

        // Act
        var act = () => _application.ReplaceMyPictureAsync(Player, "image/png", PictureSamples.Png, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<PlayerProfileNotFoundException>();
        _profiles.Verify(r => r.AddIfAbsentAsync(It.IsAny<PlayerProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        _pictures.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("mismatch")]
    [InlineData("truncated")]
    public async Task ReplaceMyPictureAsync_WithInvalidFormat_ThrowsBeforeLookup(string variant)
    {
        // Arrange
        var content = variant switch
        {
            "empty" => [],
            "mismatch" => PictureSamples.Jpeg,
            _ => PictureSamples.Png[..20],
        };

        // Act
        var act = () => _application.ReplaceMyPictureAsync(Player, "image/png", content, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidProfileException>()).Which.Field.Should().Be("content");
        _profiles.VerifyNoOtherCalls();
        _pictures.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetMyPictureAsync_WithoutProfile_ReturnsNull()
    {
        // Arrange
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync((PlayerProfile?)null);

        // Act
        var result = await _application.GetMyPictureAsync(Player, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _pictures.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetMyPictureAsync_ReturnsOwnersPicture()
    {
        // Arrange
        var stored = PlayerProfile.Create(Issuer, "player-a", "Alex", Now.AddDays(-1));
        var picture = ProfilePicture.Create(stored.Id, "image/webp", [1, 2, 3], Now);
        using var cancellation = new CancellationTokenSource();
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", cancellation.Token)).ReturnsAsync(stored);
        _pictures.Setup(s => s.GetAsync(stored.Id, cancellation.Token)).ReturnsAsync(picture);

        // Act
        var result = await _application.GetMyPictureAsync(Player, cancellation.Token);

        // Assert
        result.Should().BeSameAs(picture);
        _pictures.Verify(s => s.GetAsync(stored.Id, cancellation.Token), Times.Once);
        _pictures.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("get")]
    [InlineData("replace")]
    public async Task PictureOperations_ForMachineActor_Throw(string operation)
    {
        // Arrange
        var machine = new AuthenticatedActor(Issuer, "service-account-quizarena-gameplay", ActorKind.Machine);

        // Act
        Func<Task> act = operation == "get"
            ? () => _application.GetMyPictureAsync(machine, CancellationToken.None)
            : () => _application.ReplaceMyPictureAsync(machine, "image/png", PictureSamples.Png, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _profiles.VerifyNoOtherCalls();
        _pictures.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReplaceMyPictureAsync_WhenStoreReportsConflict_PropagatesConflict()
    {
        // Arrange
        var stored = PlayerProfile.Create(Issuer, "player-a", "Alex", Now.AddDays(-1));
        _profiles.Setup(r => r.FindByIdentityAsync(Issuer, "player-a", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _pictures.Setup(s => s.ReplaceAsync(stored.Id, "image/png", It.IsAny<byte[]>(), Now, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProfilePictureConflictException());

        // Act
        var act = () => _application.ReplaceMyPictureAsync(Player, "image/png", PictureSamples.Png, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ProfilePictureConflictException>();
    }

    [Fact]
    public void ProfileResponse_WithoutPicture_SerializesPictureAsNull()
    {
        // Arrange
        var response = new ProfileResponse(Guid.NewGuid(), Issuer, "player-a", "Alex", null);

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        // Assert
        json.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(ContractSchemaProperties("Profile"));
        json.RootElement.GetProperty("picture").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void ProfileResponse_WithPicture_SerializesOnlyContractPictureFields()
    {
        // Arrange
        var response = ProfileResponse.From(PlayerProfile.Create(Issuer, "player-a", "Alex", Now), new ProfilePictureInfo("image/jpeg", 4, Now));

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        // Assert
        var picture = json.RootElement.GetProperty("picture");
        picture.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("contentType", "updatedAt", "version");
        picture.GetProperty("contentType").GetString().Should().Be("image/jpeg");
        picture.GetProperty("version").GetInt32().Should().Be(4);
        picture.GetProperty("updatedAt").GetDateTime().Should().Be(Now);
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

    [Fact]
    public void ContractDocument_HasNoProposedMarkers()
    {
        // Arrange
        var contract = ReadContract();

        // Act
        var marked = contract.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Where(op => HasMarker(op.Value)).Select(op => $"{op.Name} {path.Name}"))
            .Concat(contract.GetProperty("components").GetProperty("schemas").EnumerateObject()
                .Where(schema => schema.Value.TryGetProperty("properties", out _))
                .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                    .Where(p => HasMarker(p.Value)).Select(p => $"{schema.Name}.{p.Name}")))
            .ToList();

        // Assert
        marked.Should().BeEmpty("#144 implemented every proposed item and removed its markers");
    }

    [Fact]
    public void ProfileContract_Picture_IsRequiredNullableWithoutStorageFields()
    {
        // Arrange
        var profile = ReadContract().GetProperty("components").GetProperty("schemas").GetProperty("Profile");

        // Act
        var required = profile.GetProperty("required").EnumerateArray().Select(r => r.GetString()).ToList();
        var picture = profile.GetProperty("properties").GetProperty("picture");

        // Assert
        required.Should().Contain("picture");
        picture.GetProperty("nullable").GetBoolean().Should().BeTrue();
        picture.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        picture.GetProperty("properties").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("contentType", "updatedAt", "version");
    }

    private static bool HasMarker(JsonElement node) =>
        node.TryGetProperty("x-status", out _) || node.TryGetProperty("x-implemented-by", out _);

    private static IReadOnlyList<string> ContractSchemaProperties(string schema) =>
        ReadContract().GetProperty("components").GetProperty("schemas").GetProperty(schema)
            .GetProperty("properties").EnumerateObject()
            .Select(p => p.Name).ToList();

    private static JsonElement ReadContract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory!.FullName, "contracts", "openapi", "identity.yaml");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
