using FluentAssertions;
using QuizArena.Lobby.Domain.Entities;
using QuizArena.Lobby.Domain.Exceptions;
using QuizArena.Lobby.Domain.Services;
using Xunit;

namespace QuizArena.Lobby.UnitTests.Domain;

public sealed class JoinCodeGeneratorTests
{
    [Fact]
    public void Generate_NewCandidate_HasSixUppercaseAsciiAlphanumericCharacters()
    {
        // Arrange
        // Act
        var code = JoinCodeGenerator.Generate();

        // Assert
        code.Should().MatchRegex("^[A-Z0-9]{6}$");
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("AAAAAA")]
    [InlineData("999999")]
    public void Validate_ValidCode_PreservesExactValue(string code)
    {
        // Arrange
        // Act
        var result = JoinCodeGenerator.Validate(code);

        // Assert
        result.Should().Be(code);
        JoinCodeGenerator.IsValid(result).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    [InlineData("abc123")]
    [InlineData("AbC123")]
    [InlineData(" ABC12")]
    [InlineData("ABC12 ")]
    [InlineData("ABC12\n")]
    [InlineData("ABC-12")]
    [InlineData("ＡBC123")]
    [InlineData("ABC１２３")]
    public void Validate_InvalidCode_RejectsWithoutNormalization(string? code)
    {
        // Arrange
        // Act
        Action act = () => JoinCodeGenerator.Validate(code!);

        // Assert
        act.Should().Throw<InvalidLobbyException>().Which.Field.Should().Be("joinCode");
        JoinCodeGenerator.IsValid(code).Should().BeFalse();
    }

    [Fact]
    public void Create_InvalidCode_RejectsAggregateCreation()
    {
        // Arrange
        // Act
        Action act = () => ActiveLobby.Create("host", "abc123");

        // Assert
        act.Should().Throw<InvalidLobbyException>().Which.Field.Should().Be("joinCode");
    }
}
