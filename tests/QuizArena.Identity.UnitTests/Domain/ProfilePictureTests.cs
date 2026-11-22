using FluentAssertions;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Exceptions;
using Xunit;

namespace QuizArena.Identity.UnitTests.Domain;

public sealed class ProfilePictureTests
{
    private static readonly Guid ProfileId = Guid.Parse("4b0e3a52-9c3f-4f39-9a59-2f3d5d0a7c11");
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Create_WithValidInput_StartsAtVersionOne()
    {
        // Act
        var picture = ProfilePicture.Create(ProfileId, "image/png", Png, Now);

        // Assert
        picture.ProfileId.Should().Be(ProfileId);
        picture.ContentType.Should().Be("image/png");
        picture.Content.Should().Equal(Png);
        picture.Version.Should().Be(1);
        picture.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void Replace_IncrementsVersionByOneAndReplacesFields()
    {
        // Arrange
        var picture = ProfilePicture.Create(ProfileId, "image/png", Png, Now);
        var later = Now.AddMinutes(5);

        // Act
        picture.Replace("image/webp", [1, 2, 3], later);
        picture.Replace("image/jpeg", [4, 5], later.AddMinutes(1));

        // Assert
        picture.Version.Should().Be(3);
        picture.ContentType.Should().Be("image/jpeg");
        picture.Content.Should().Equal(4, 5);
        picture.UpdatedAt.Should().Be(later.AddMinutes(1));
        picture.ProfileId.Should().Be(ProfileId);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public void Create_WithEachAllowedContentType_Succeeds(string contentType)
    {
        // Act
        var picture = ProfilePicture.Create(ProfileId, contentType, Png, Now);

        // Assert
        picture.ContentType.Should().Be(contentType);
        ProfilePicture.AllowedContentTypes.Should().BeEquivalentTo("image/png", "image/jpeg", "image/webp");
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("image/gif")]
    [InlineData("IMAGE/PNG")]
    [InlineData("")]
    [InlineData(" image/png")]
    [InlineData(null)]
    public void Create_WithDisallowedContentType_Throws(string? contentType)
    {
        // Act
        var act = () => ProfilePicture.Create(ProfileId, contentType!, Png, Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("contentType");
    }

    [Fact]
    public void Create_WithContentAtExactMax_Succeeds()
    {
        // Act
        var picture = ProfilePicture.Create(ProfileId, "image/png", new byte[ProfilePicture.MaxContentLength], Now);

        // Assert
        picture.Content.Should().HaveCount(2_097_152);
    }

    [Fact]
    public void Create_WithContentOverMax_Throws()
    {
        // Act
        var act = () => ProfilePicture.Create(ProfileId, "image/png", new byte[ProfilePicture.MaxContentLength + 1], Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("content");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_WithEmptyContent_Throws(bool isNull)
    {
        // Act
        var act = () => ProfilePicture.Create(ProfileId, "image/png", isNull ? null! : [], Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("content");
    }

    [Fact]
    public void Create_WithEmptyProfileId_Throws()
    {
        // Act
        var act = () => ProfilePicture.Create(Guid.Empty, "image/png", Png, Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("profileId");
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Create_WithNonUtcTimestamp_Throws(DateTimeKind kind)
    {
        // Act
        var act = () => ProfilePicture.Create(ProfileId, "image/png", Png, DateTime.SpecifyKind(Now, kind));

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("updatedAt");
    }

    [Fact]
    public void Create_WithDefaultTimestamp_Throws()
    {
        // Act
        var act = () => ProfilePicture.Create(ProfileId, "image/png", Png, default);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("updatedAt");
    }

    [Theory]
    [InlineData("image/gif", 3, "utc")]
    [InlineData("image/png", 0, "utc")]
    [InlineData("image/png", 3, "local")]
    public void Replace_WithInvalidInput_LeavesPictureUnchanged(string contentType, int length, string kind)
    {
        // Arrange
        var picture = ProfilePicture.Create(ProfileId, "image/png", Png, Now);
        var timestamp = kind == "utc" ? Now.AddMinutes(1) : DateTime.SpecifyKind(Now.AddMinutes(1), DateTimeKind.Local);

        // Act
        var act = () => picture.Replace(contentType, new byte[length], timestamp);

        // Assert
        act.Should().Throw<InvalidProfileException>();
        picture.Version.Should().Be(1);
        picture.ContentType.Should().Be("image/png");
        picture.Content.Should().Equal(Png);
        picture.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_CopiesContent_CallerMutationDoesNotLeak()
    {
        // Arrange
        var buffer = (byte[])Png.Clone();
        var picture = ProfilePicture.Create(ProfileId, "image/png", buffer, Now);
        var replacement = new byte[] { 9, 9, 9 };

        // Act
        buffer[0] = 0x00;
        picture.Replace("image/png", replacement, Now.AddMinutes(1));
        replacement[0] = 0x01;

        // Assert
        picture.Content.Should().Equal(9, 9, 9);
        picture.Content.Should().NotBeSameAs(replacement);
    }

    [Theory]
    [InlineData("image/png", 0, DateTimeKind.Utc)]
    [InlineData("image/png", -1, DateTimeKind.Utc)]
    [InlineData("image/png", 1, DateTimeKind.Unspecified)]
    [InlineData("image/svg+xml", 1, DateTimeKind.Utc)]
    public void Rehydrate_WithCorruptValues_Throws(string contentType, int version, DateTimeKind kind)
    {
        // Act
        var act = () => ProfilePicture.Rehydrate(ProfileId, contentType, Png, version, DateTime.SpecifyKind(Now, kind));

        // Assert
        act.Should().Throw<InvalidProfileException>();
    }

    [Fact]
    public void Rehydrate_WithValidRow_RoundTrips()
    {
        // Act
        var picture = ProfilePicture.Rehydrate(ProfileId, "image/jpeg", Png, 7, Now);

        // Assert
        picture.ToInfo().Should().Be(new ProfilePictureInfo("image/jpeg", 7, Now));
        picture.Content.Should().Equal(Png);
        picture.ProfileId.Should().Be(ProfileId);
    }

    [Fact]
    public void Rehydrate_WithEmptyContent_Throws()
    {
        // Act
        var act = () => ProfilePicture.Rehydrate(ProfileId, "image/png", [], 1, Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("content");
    }

    [Theory]
    [InlineData("image/png", 0, DateTimeKind.Utc)]
    [InlineData("image/png", 1, DateTimeKind.Local)]
    [InlineData("image/gif", 1, DateTimeKind.Utc)]
    public void RehydrateInfo_WithCorruptValues_Throws(string contentType, int version, DateTimeKind kind)
    {
        // Act
        var act = () => ProfilePicture.RehydrateInfo(contentType, version, DateTime.SpecifyKind(Now, kind));

        // Assert
        act.Should().Throw<InvalidProfileException>();
    }

    [Fact]
    public void RehydrateInfo_WithValidValues_ReturnsReference()
    {
        // Act
        var info = ProfilePicture.RehydrateInfo("image/webp", 4, Now);

        // Assert
        info.Should().Be(new ProfilePictureInfo("image/webp", 4, Now));
    }

    [Fact]
    public void ProfilePictureInfo_ExposesOnlyContentTypeVersionAndUpdatedAt()
    {
        // Act
        var properties = typeof(ProfilePictureInfo).GetProperties().Select(p => p.Name);

        // Assert (SEC-03: no storage key, path, URL or bytes in the reference)
        properties.Should().BeEquivalentTo("ContentType", "Version", "UpdatedAt");
    }
}
