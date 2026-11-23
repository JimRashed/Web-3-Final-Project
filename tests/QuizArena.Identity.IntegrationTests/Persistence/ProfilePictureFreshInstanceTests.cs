using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Identity.Api.Contracts;
using QuizArena.Identity.Api.Security;
using QuizArena.Identity.Api.Services;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Repositories;
using QuizArena.Identity.IntegrationTests.Endpoints;
using QuizArena.Identity.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Persistence;

/// <summary>
/// Two independently built Identity hosts (own DI container and DbContext pool) sharing one SQL Server database:
/// a picture saved by one service instance is read back by a fresh instance.
/// </summary>
public sealed class ProfilePictureFreshInstanceTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public ProfilePictureFreshInstanceTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReplaceMyPicture_InFirstHost_IsReadableFromSecondHost()
    {
        // Arrange
        var actor = new AuthenticatedActor(TestTokens.Issuer, $"player-{Guid.NewGuid():N}", ActorKind.User);
        var content = PictureSamples.WebpLossless;
        ProfilePictureResponse saved;
        await using (var firstHost = _factory.WithWebHostBuilder(_ => { }))
        {
            await using var scope = firstHost.Services.CreateAsyncScope();
            var application = scope.ServiceProvider.GetRequiredService<IProfileApplication>();
            await application.UpsertMeAsync(actor, new UpdateProfileRequest("Alex"), CancellationToken.None);
            saved = (await application.ReplaceMyPictureAsync(actor, "image/webp", content, CancellationToken.None)).Picture!;
        }

        // Act
        await using var secondHost = _factory.WithWebHostBuilder(_ => { });
        await using var readScope = secondHost.Services.CreateAsyncScope();
        var reader = readScope.ServiceProvider.GetRequiredService<IProfileApplication>();
        var picture = await reader.GetMyPictureAsync(actor, CancellationToken.None);
        var profile = await reader.GetMeAsync(actor, CancellationToken.None);
        var info = await readScope.ServiceProvider.GetRequiredService<IProfilePictureStore>().GetInfoAsync(profile!.Id, CancellationToken.None);

        // Assert
        picture.Should().NotBeNull();
        picture!.Content.Should().Equal(content);
        picture.ContentType.Should().Be("image/webp");
        picture.Version.Should().Be(1);
        picture.UpdatedAt.Should().Be(saved.UpdatedAt);
        picture.ProfileId.Should().Be(profile.Id);
        info.Should().Be(new ProfilePictureInfo(saved.ContentType, saved.Version, saved.UpdatedAt));
        profile.Picture.Should().Be(saved);
    }

    [Fact]
    public async Task ReplaceMyPicture_ForCaseVariantSubjects_KeepsPicturesSeparate()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");
        var lower = new AuthenticatedActor(TestTokens.Issuer, $"subject-x-{suffix}", ActorKind.User);
        var upper = new AuthenticatedActor(TestTokens.Issuer, $"Subject-X-{suffix}", ActorKind.User);
        await using (var host = _factory.WithWebHostBuilder(_ => { }))
        {
            await using var scope = host.Services.CreateAsyncScope();
            var application = scope.ServiceProvider.GetRequiredService<IProfileApplication>();
            await application.UpsertMeAsync(lower, new UpdateProfileRequest("Lower"), CancellationToken.None);
            await application.UpsertMeAsync(upper, new UpdateProfileRequest("Upper"), CancellationToken.None);
            await application.ReplaceMyPictureAsync(lower, "image/png", PictureSamples.Png, CancellationToken.None);
            await application.ReplaceMyPictureAsync(upper, "image/jpeg", PictureSamples.Jpeg, CancellationToken.None);
        }

        // Act
        await using var freshHost = _factory.WithWebHostBuilder(_ => { });
        await using var readScope = freshHost.Services.CreateAsyncScope();
        var reader = readScope.ServiceProvider.GetRequiredService<IProfileApplication>();
        var lowerPicture = await reader.GetMyPictureAsync(lower, CancellationToken.None);
        var upperPicture = await reader.GetMyPictureAsync(upper, CancellationToken.None);

        // Assert
        lowerPicture!.Content.Should().Equal(PictureSamples.Png);
        lowerPicture.ContentType.Should().Be("image/png");
        upperPicture!.Content.Should().Equal(PictureSamples.Jpeg);
        upperPicture.ContentType.Should().Be("image/jpeg");
        lowerPicture.ProfileId.Should().NotBe(upperPicture.ProfileId);
    }
}
