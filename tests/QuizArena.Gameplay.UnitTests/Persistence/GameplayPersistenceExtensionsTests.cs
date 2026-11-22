using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Gameplay.Data;
using Xunit;

namespace QuizArena.Gameplay.UnitTests.Persistence;

/// <summary>
/// SEC-A2 (#138): the connection-string check shared in shape by all six <c>*PersistenceExtensions</c>.
/// A malformed value is refused while registering, without opening a connection, and the error names
/// the key only. SqlClient's own parse error is not chained, because it can quote part of the value.
/// </summary>
public sealed class GameplayPersistenceExtensionsTests
{
    private static readonly string Sentinel = $"sentinel-{Guid.NewGuid():N}";

    [Theory]
    [InlineData("{0}=value")]
    [InlineData("Server=db;{0}")]
    [InlineData("Server=db;Connect Timeout={0}")]
    [InlineData("Server=db;Max Pool Size=99999999999{0}")]
    public void AddGameplayPersistence_WithMalformedConnectionString_ThrowsNamingTheKeyOnly(string template)
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = string.Format(CultureInfo.InvariantCulture, template, Sentinel);

        // Act
        var act = () => services.AddGameplayPersistence(connectionString);

        // Assert
        var thrown = act.Should().Throw<InvalidOperationException>().Which;
        thrown.Message.Should().Contain("ConnectionStrings:DefaultConnection").And.NotContain(Sentinel);
        thrown.InnerException.Should().BeNull();
        services.Should().BeEmpty();
    }

    [Fact]
    public void AddGameplayPersistence_WithWellFormedConnectionString_RegistersWithoutConnecting()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGameplayPersistence("Server=unreachable.example.test;Database=QuizArenaGameplay;Integrated Security=True;Encrypt=True");

        // Assert
        services.Should().NotBeEmpty();
    }
}
