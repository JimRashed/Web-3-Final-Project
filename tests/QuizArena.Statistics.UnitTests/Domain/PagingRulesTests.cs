using FluentAssertions;
using QuizArena.Statistics.Domain.Exceptions;
using QuizArena.Statistics.Domain.Services;
using Xunit;

namespace QuizArena.Statistics.UnitTests.Domain;

public sealed class PagingRulesTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, StatisticsCalculator.DefaultPageSize)]
    [InlineData(500, StatisticsCalculator.MaxPageSize)]
    public void ValidatePaging_WithinBounds_DoesNotThrow(int page, int pageSize)
    {
        // Act
        var act = () => StatisticsCalculator.ValidatePaging(page, pageSize);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, 20, "page")]
    [InlineData(-1, 20, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, StatisticsCalculator.MaxPageSize + 1, "pageSize")]
    public void ValidatePaging_OutOfBounds_ThrowsWithField(int page, int pageSize, string field)
    {
        // Act
        var act = () => StatisticsCalculator.ValidatePaging(page, pageSize);

        // Assert
        act.Should().Throw<InvalidMatchResultException>().Which.Field.Should().Be(field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(StatisticsCalculator.MaxLeaderboardSize + 1)]
    public void ValidateTop_OutOfBounds_Throws(int top)
    {
        // Act
        var act = () => StatisticsCalculator.ValidateTop(top);

        // Assert
        act.Should().Throw<InvalidMatchResultException>().Which.Field.Should().Be("top");
    }
}
