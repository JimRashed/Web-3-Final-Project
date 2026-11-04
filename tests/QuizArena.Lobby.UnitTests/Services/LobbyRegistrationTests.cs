using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using QuizArena.Lobby.Api;
using QuizArena.Lobby.Api.Services;
using QuizArena.Lobby.Data.Repositories;
using Xunit;

namespace QuizArena.Lobby.UnitTests.Services;

public sealed class LobbyRegistrationTests
{
    [Fact]
    public void Registration_ResolvedInScopes_UsesScopedApplicationServiceAndConstructorInjection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<ILobbyRepository>(MockBehavior.Strict).Object);
        services.AddLobbyApplication();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        // Act
        var service = first.ServiceProvider.GetRequiredService<ILobbyService>();

        // Assert
        service.Should().BeOfType<LobbyService>();
        service.Should().BeSameAs(first.ServiceProvider.GetRequiredService<ILobbyService>());
        service.Should().NotBeSameAs(second.ServiceProvider.GetRequiredService<ILobbyService>());
    }
}
