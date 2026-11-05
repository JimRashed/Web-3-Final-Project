using FluentAssertions;
using Microsoft.Extensions.Configuration;
using QuizArena.Gameplay.Api.Services;
using Xunit;

namespace QuizArena.Gameplay.UnitTests.Services;

/// <summary>
/// The machine token client must read the documented keys. A property name that differs from the
/// configuration key binds silently to an empty value, and every service call then fails with 503.
/// </summary>
public sealed class MachineTokenOptionsBindingTests
{
    [Fact]
    public void Bind_CommittedAppSettingsPlusEnvironmentSecret_PopulatesClientIdAndSecret()
    {
        // Arrange: the committed appsettings.json plus the secret as the environment supplies it.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(FindRepositoryRoot(), "src", "QuizArena.Gameplay.Api", "appsettings.json"))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "http://localhost:8080/realms/quizarena",
                ["Authentication:MachineClientSecret"] = "from-environment",
            })
            .Build();

        // Act
        var options = configuration.GetSection(MachineTokenOptions.SectionName).Get<MachineTokenOptions>()!;

        // Assert
        options.MachineClientId.Should().Be("quizarena-gameplay");
        options.MachineClientSecret.Should().Be("from-environment");
        options.Authority.Should().Be("http://localhost:8080/realms/quizarena");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("QuizArena.sln not found.");
    }
}
