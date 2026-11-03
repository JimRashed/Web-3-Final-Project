using FluentAssertions;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Exceptions;
using Xunit;

namespace QuizArena.Identity.UnitTests.Domain;

public sealed class PlayerProfileTests
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private const string Subject = "8f7d2a15-2e7a-4a7d-8c3a-0f4e4d3f9a21";
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidClaims_SetsIdentityNameAndTimestamps()
    {
        // Act
        var profile = PlayerProfile.Create(Issuer, Subject, "  Alex  ", Now);

        // Assert
        profile.Id.Should().NotBeEmpty();
        profile.Issuer.Should().Be(Issuer);
        profile.Subject.Should().Be(Subject);
        profile.DisplayName.Should().Be("Alex");
        profile.CreatedAt.Should().Be(Now);
        profile.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_WithSuppliedId_KeepsId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var profile = PlayerProfile.Create(Issuer, Subject, "Alex", Now, id);

        // Assert
        profile.Id.Should().Be(id);
    }

    [Fact]
    public void Create_WithMixedCaseSubject_PreservesSubjectExactly()
    {
        // Act
        var profile = PlayerProfile.Create(Issuer, "Player-A|xYz", "Alex", Now);

        // Assert
        profile.Subject.Should().Be("Player-A|xYz");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" player-a")]
    [InlineData("player-a ")]
    public void Create_WithBlankOrPaddedSubject_ThrowsInvalidProfile(string subject)
    {
        // Act
        var act = () => PlayerProfile.Create(Issuer, subject, "Alex", Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("subject");
    }

    [Fact]
    public void Create_WithSubjectAtMaxLength_Succeeds()
    {
        // Act
        var profile = PlayerProfile.Create(Issuer, new string('s', PlayerProfile.MaxSubjectLength), "Alex", Now);

        // Assert
        profile.Subject.Should().HaveLength(PlayerProfile.MaxSubjectLength);
    }

    [Fact]
    public void Create_WithSubjectOverMaxLength_ThrowsInvalidProfile()
    {
        // Act
        var act = () => PlayerProfile.Create(Issuer, new string('s', PlayerProfile.MaxSubjectLength + 1), "Alex", Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("subject");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("quizarena")]
    [InlineData("ftp://localhost/realms/quizarena")]
    [InlineData(" http://localhost:8080/realms/quizarena")]
    public void Create_WithInvalidIssuer_ThrowsInvalidProfile(string issuer)
    {
        // Act
        var act = () => PlayerProfile.Create(issuer, Subject, "Alex", Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("issuer");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Al\nex")]
    [InlineData("Alex\u0007")]
    public void Create_WithBlankOrControlCharacterDisplayName_ThrowsInvalidProfile(string displayName)
    {
        // Act
        var act = () => PlayerProfile.Create(Issuer, Subject, displayName, Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("displayName");
    }

    [Fact]
    public void Create_WithDisplayNameAtMaxLengthAfterTrim_Succeeds()
    {
        // Act
        var profile = PlayerProfile.Create(Issuer, Subject, $"  {new string('a', PlayerProfile.MaxDisplayNameLength)}  ", Now);

        // Assert
        profile.DisplayName.Should().HaveLength(PlayerProfile.MaxDisplayNameLength);
    }

    [Fact]
    public void Create_WithDisplayNameOverMaxLength_ThrowsInvalidProfile()
    {
        // Act
        var act = () => PlayerProfile.Create(Issuer, Subject, new string('a', PlayerProfile.MaxDisplayNameLength + 1), Now);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("displayName");
    }

    [Fact]
    public void Create_WithNonUtcTimestamp_ThrowsInvalidProfile()
    {
        // Act
        var act = () => PlayerProfile.Create(Issuer, Subject, "Alex", DateTime.SpecifyKind(Now, DateTimeKind.Local));

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("createdAt");
    }

    [Fact]
    public void Create_WithEmptyId_ThrowsInvalidProfile()
    {
        // Act
        var act = () => PlayerProfile.Create(Issuer, Subject, "Alex", Now, Guid.Empty);

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("id");
    }

    [Fact]
    public void Rename_WithNewName_UpdatesNameAndTimestampAndKeepsId()
    {
        // Arrange
        var profile = PlayerProfile.Create(Issuer, Subject, "Alex", Now);
        var id = profile.Id;
        var later = Now.AddMinutes(5);

        // Act
        var changed = profile.Rename(" Sam ", later);

        // Assert
        changed.Should().BeTrue();
        profile.DisplayName.Should().Be("Sam");
        profile.UpdatedAt.Should().Be(later);
        profile.Id.Should().Be(id);
        profile.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Rename_WithSameNormalizedName_IsIdempotentNoChange()
    {
        // Arrange
        var profile = PlayerProfile.Create(Issuer, Subject, "Alex", Now);

        // Act
        var changed = profile.Rename("  Alex ", Now.AddMinutes(5));

        // Assert
        changed.Should().BeFalse();
        profile.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void Rename_WithDifferentCase_TreatsAsChange()
    {
        // Arrange
        var profile = PlayerProfile.Create(Issuer, Subject, "alex", Now);

        // Act
        var changed = profile.Rename("Alex", Now.AddMinutes(1));

        // Assert
        changed.Should().BeTrue();
        profile.DisplayName.Should().Be("Alex");
    }

    [Fact]
    public void Rename_WithInvalidName_ThrowsAndKeepsPreviousName()
    {
        // Arrange
        var profile = PlayerProfile.Create(Issuer, Subject, "Alex", Now);

        // Act
        var act = () => profile.Rename("   ", Now.AddMinutes(1));

        // Assert
        act.Should().Throw<InvalidProfileException>();
        profile.DisplayName.Should().Be("Alex");
    }

    [Fact]
    public void Rename_WithTimestampBeforeCreation_ThrowsInvalidProfile()
    {
        // Arrange
        var profile = PlayerProfile.Create(Issuer, Subject, "Alex", Now);

        // Act
        var act = () => profile.Rename("Sam", Now.AddMinutes(-1));

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("updatedAt");
    }

    [Fact]
    public void Rehydrate_WithStoredValues_RestoresProfile()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var profile = PlayerProfile.Rehydrate(id, Issuer, Subject, "Alex", Now, Now.AddDays(1));

        // Assert
        profile.Id.Should().Be(id);
        profile.UpdatedAt.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void Rehydrate_WithUpdatedBeforeCreated_ThrowsInvalidProfile()
    {
        // Act
        var act = () => PlayerProfile.Rehydrate(Guid.NewGuid(), Issuer, Subject, "Alex", Now, Now.AddDays(-1));

        // Assert
        act.Should().Throw<InvalidProfileException>().Which.Field.Should().Be("updatedAt");
    }

    [Theory]
    [InlineData(Issuer, Subject, true)]
    [InlineData(Issuer, "8F7D2A15-2E7A-4A7D-8C3A-0F4E4D3F9A21", false)]
    [InlineData("http://localhost:8080/realms/other", Subject, false)]
    [InlineData(Issuer + "/", Subject, false)]
    public void BelongsTo_ComparesIssuerAndSubjectExactly(string issuer, string subject, bool expected)
    {
        // Arrange
        var profile = PlayerProfile.Create(Issuer, Subject, "Alex", Now);

        // Act
        var result = profile.BelongsTo(issuer, subject);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("player-a", "player-a")]
    [InlineData("  alex  ", "alex")]
    [InlineData(null, PlayerProfile.FallbackDisplayName)]
    [InlineData("   ", PlayerProfile.FallbackDisplayName)]
    [InlineData("bad\tname", PlayerProfile.FallbackDisplayName)]
    public void SuggestDisplayName_FromHint_ReturnsUsableName(string? hint, string expected)
    {
        // Act
        var suggestion = PlayerProfile.SuggestDisplayName(hint);

        // Assert
        suggestion.Should().Be(expected);
    }

    [Fact]
    public void SuggestDisplayName_WithLongHint_TruncatesToValidName()
    {
        // Act
        var suggestion = PlayerProfile.SuggestDisplayName(new string('x', 200));

        // Assert
        suggestion.Should().HaveLength(PlayerProfile.MaxDisplayNameLength);
        PlayerProfile.Create(Issuer, Subject, suggestion, Now).DisplayName.Should().Be(suggestion);
    }
}
