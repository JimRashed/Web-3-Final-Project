using FluentAssertions;
using QuizArena.Statistics.Domain.Entities;
using QuizArena.Statistics.Domain.Exceptions;
using Xunit;

namespace QuizArena.Statistics.UnitTests.Domain;

public sealed class MatchResultTests
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private const string MatchId = "26c94137-d660-4397-a731-050c2bb07001";
    private static readonly DateTime PlayedAt = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Record_WithValidValues_SetsAllFields()
    {
        // Act
        var result = MatchResult.Record(Issuer, MatchId, "player-a", 3, true, PlayedAt);

        // Assert
        result.Issuer.Should().Be(Issuer);
        result.MatchId.Should().Be(MatchId);
        result.UserId.Should().Be("player-a");
        result.Score.Should().Be(3);
        result.Won.Should().BeTrue();
        result.PlayedAt.Should().Be(PlayedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MatchResult.MaxNewScore)]
    public void Record_WithScoreAtBounds_Succeeds(int score)
    {
        // Act
        var result = MatchResult.Record(Issuer, MatchId, "player-a", score, false, PlayedAt);

        // Assert
        result.Score.Should().Be(score);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(MatchResult.MaxNewScore + 1)]
    public void Record_WithScoreOutOfRange_ThrowsInvalidMatchResult(int score)
    {
        // Act
        var act = () => MatchResult.Record(Issuer, MatchId, "player-a", score, false, PlayedAt);

        // Assert
        act.Should().Throw<InvalidMatchResultException>().Which.Field.Should().Be("score");
    }

    [Fact]
    public void Rehydrate_WithHistoricalScoreAboveFive_Succeeds()
    {
        // Act
        var result = MatchResult.Rehydrate(Issuer, "legacy-match-7", "player-a", 12, true, PlayedAt);

        // Assert
        result.Score.Should().Be(12);
        result.MatchId.Should().Be("legacy-match-7");
    }

    [Fact]
    public void Rehydrate_WithNegativeScore_ThrowsInvalidMatchResult()
    {
        // Act
        var act = () => MatchResult.Rehydrate(Issuer, MatchId, "player-a", -1, false, PlayedAt);

        // Assert
        act.Should().Throw<InvalidMatchResultException>().Which.Field.Should().Be("score");
    }

    [Theory]
    [InlineData("", "player-a", "matchId")]
    [InlineData("   ", "player-a", "matchId")]
    [InlineData(" " + MatchId, "player-a", "matchId")]
    [InlineData(MatchId, "", "userId")]
    [InlineData(MatchId, "player-a ", "userId")]
    public void Record_WithBlankOrPaddedIdentifier_ThrowsInvalidMatchResult(string matchId, string userId, string field)
    {
        // Act
        var act = () => MatchResult.Record(Issuer, matchId, userId, 1, false, PlayedAt);

        // Assert
        act.Should().Throw<InvalidMatchResultException>().Which.Field.Should().Be(field);
    }

    [Fact]
    public void Record_WithMatchIdOverMaxLength_ThrowsInvalidMatchResult()
    {
        // Act
        var act = () => MatchResult.Record(Issuer, new string('m', MatchResult.MaxMatchIdLength + 1), "player-a", 1, false, PlayedAt);

        // Assert
        act.Should().Throw<InvalidMatchResultException>().Which.Field.Should().Be("matchId");
    }

    [Fact]
    public void Record_WithUserIdAtMaxLength_Succeeds()
    {
        // Act
        var result = MatchResult.Record(Issuer, MatchId, new string('u', MatchResult.MaxUserIdLength), 1, false, PlayedAt);

        // Assert
        result.UserId.Should().HaveLength(MatchResult.MaxUserIdLength);
    }

    [Fact]
    public void Record_WithBlankIssuer_ThrowsInvalidMatchResult()
    {
        // Act
        var act = () => MatchResult.Record(" ", MatchId, "player-a", 1, false, PlayedAt);

        // Assert
        act.Should().Throw<InvalidMatchResultException>().Which.Field.Should().Be("issuer");
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Record_WithNonUtcPlayedAt_ThrowsInvalidMatchResult(DateTimeKind kind)
    {
        // Act
        var act = () => MatchResult.Record(Issuer, MatchId, "player-a", 1, false, DateTime.SpecifyKind(PlayedAt, kind));

        // Assert
        act.Should().Throw<InvalidMatchResultException>().Which.Field.Should().Be("playedAt");
    }

    [Fact]
    public void IsSameResultAs_WithIdenticalValues_ReturnsTrue()
    {
        // Arrange
        var first = MatchResult.Record(Issuer, MatchId, "player-a", 3, true, PlayedAt);
        var replay = MatchResult.Record(Issuer, MatchId, "player-a", 3, true, PlayedAt);

        // Act + Assert
        first.IsSameResultAs(replay).Should().BeTrue();
    }

    [Theory]
    [InlineData(4, true, 0)]
    [InlineData(3, false, 0)]
    [InlineData(3, true, 1)]
    public void IsSameResultAs_WithChangedScoreOutcomeOrTime_ReturnsFalse(int score, bool won, int minutesLater)
    {
        // Arrange
        var stored = MatchResult.Record(Issuer, MatchId, "player-a", 3, true, PlayedAt);
        var incoming = MatchResult.Record(Issuer, MatchId, "player-a", score, won, PlayedAt.AddMinutes(minutesLater));

        // Act + Assert
        stored.IsSameResultAs(incoming).Should().BeFalse();
        stored.HasSameKeyAs(incoming).Should().BeTrue();
    }

    [Theory]
    [InlineData("http://localhost:8080/realms/other", MatchId, "player-a")]
    [InlineData(Issuer, "26C94137-D660-4397-A731-050C2BB07001", "player-a")]
    [InlineData(Issuer, MatchId, "Player-A")]
    public void HasSameKeyAs_ComparesKeyExactly(string issuer, string matchId, string userId)
    {
        // Arrange
        var stored = MatchResult.Record(Issuer, MatchId, "player-a", 3, true, PlayedAt);
        var other = MatchResult.Record(issuer, matchId, userId, 3, true, PlayedAt);

        // Act + Assert
        stored.HasSameKeyAs(other).Should().BeFalse();
    }
}
