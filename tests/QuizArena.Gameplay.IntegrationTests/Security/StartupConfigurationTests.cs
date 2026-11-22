using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuizArena.Gameplay.Api.Services.Outbox;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Security;

/// <summary>
/// SEC-A2 (#138): an absent or invalid required setting stops the host at startup, and the failure
/// names the key without echoing the configured value. Gameplay is the representative host for the
/// shared <c>Security/SecurityExtensions.cs</c> rules (identical on all six APIs) and for its own
/// service-address and machine-secret rules. Each Production case starts from a complete valid
/// baseline and breaks exactly one key.
/// </summary>
public sealed class StartupConfigurationTests
{
    private static readonly string Sentinel = $"sentinel-{Guid.NewGuid():N}";

    [Fact]
    public async Task Startup_ProductionWithCompleteConfiguration_ServesHealth()
    {
        // Arrange
        using var factory = new ConfiguredGameplayFactory("Production", ValidProduction());
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Startup_DevelopmentWithoutMachineSecret_StillStarts()
    {
        // Arrange
        var settings = ValidProduction();
        settings["Authentication:MachineClientSecret"] = string.Empty;
        using var factory = new ConfiguredGameplayFactory("Development", settings);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Startup_ProductionWithoutMachineSecret_FailsNamingTheKey()
    {
        // Arrange
        var settings = ValidProduction();
        settings["Authentication:MachineClientSecret"] = "   ";

        // Act
        var failure = StartupFailure("Production", settings);

        // Assert
        failure.Should().Contain("Authentication:MachineClientSecret");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Startup_WithNonHttpAuthority_FailsNamingTheKeyOnly(string environment)
    {
        // Arrange
        var settings = ValidProduction();
        settings["Authentication:Authority"] = $"ftp://{Sentinel}.example.test/realms/quizarena";

        // Act
        var failure = StartupFailure(environment, settings);

        // Assert
        failure.Should().Contain("Authentication:Authority").And.NotContain(Sentinel);
    }

    [Fact]
    public void Startup_ProductionWithPlainHttpAuthority_FailsNamingTheKeyOnly()
    {
        // Arrange
        var settings = ValidProduction();
        settings["Authentication:Authority"] = $"http://{Sentinel}.example.test/realms/quizarena";

        // Act
        var failure = StartupFailure("Production", settings);

        // Assert
        failure.Should().Contain("Authentication:Authority must use https").And.NotContain(Sentinel);
    }

    [Fact]
    public void Startup_ProductionWithoutHttpsMetadata_FailsNamingTheKey()
    {
        // Arrange
        var settings = ValidProduction();
        settings["Authentication:RequireHttpsMetadata"] = "false";

        // Act
        var failure = StartupFailure("Production", settings);

        // Assert
        failure.Should().Contain("Authentication:RequireHttpsMetadata");
    }

    [Fact]
    public void Startup_ProductionWithoutCorsOrigins_FailsNamingTheKey()
    {
        // Arrange
        var settings = ValidProduction();
        settings.Remove("Cors:AllowedOrigins:0");

        // Act
        var failure = StartupFailure("Production", settings);

        // Assert
        failure.Should().Contain("Cors:AllowedOrigins");
    }

    [Fact]
    public void Startup_ProductionWithInvalidCorsOrigin_FailsNamingTheKeyOnly()
    {
        // Arrange
        var settings = ValidProduction();
        settings["Cors:AllowedOrigins:0"] = Sentinel;

        // Act
        var failure = StartupFailure("Production", settings);

        // Assert
        failure.Should().Contain("Cors:AllowedOrigins").And.NotContain(Sentinel);
    }

    [Theory]
    [InlineData("ServiceUrls:Lobby")]
    [InlineData("ServiceUrls:QuestionBank")]
    [InlineData("ServiceUrls:Statistics")]
    [InlineData("ServiceUrls:Notification")]
    public void Startup_WithInvalidServiceUrl_FailsNamingTheKeyOnly(string key)
    {
        // Arrange
        var settings = ValidProduction();
        settings[key] = Sentinel;

        // Act
        var failure = StartupFailure("Production", settings);

        // Assert
        failure.Should().Contain(key).And.NotContain(Sentinel);
    }

    [Fact]
    public void Startup_WithoutConnectionString_FailsNamingTheKey()
    {
        // Arrange
        var settings = ValidProduction();
        settings["ConnectionStrings:DefaultConnection"] = string.Empty;

        // Act
        var failure = StartupFailure("Production", settings);

        // Assert
        failure.Should().Contain("ConnectionStrings:DefaultConnection");
    }

    [Fact]
    public void Startup_WithInvalidSettings_NeverEchoesTheMachineSecret()
    {
        // Arrange
        var settings = ValidProduction();
        settings["Authentication:Authority"] = "not-a-uri";
        settings["ServiceUrls:Lobby"] = "relative/path";

        // Act
        var failure = StartupFailure("Production", settings);

        // Assert
        failure.Should().Contain("Authentication:Authority").And.Contain("ServiceUrls:Lobby");
        failure.Should().NotContain(settings["Authentication:MachineClientSecret"]!);
    }

    private static Dictionary<string, string?> ValidProduction() => new(StringComparer.Ordinal)
    {
        ["Authentication:Authority"] = "https://idp.example.test/realms/quizarena",
        ["Authentication:RequireHttpsMetadata"] = "true",
        ["Authentication:MachineClientSecret"] = $"secret-{Guid.NewGuid():N}",
        ["Cors:AllowedOrigins:0"] = "https://app.example.test",
        ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaGameplayStartupTests;Trusted_Connection=True;TrustServerCertificate=True",
        ["ServiceUrls:Lobby"] = "https://lobby.example.test",
        ["ServiceUrls:QuestionBank"] = "https://questionbank.example.test",
        ["ServiceUrls:Statistics"] = "https://statistics.example.test",
        ["ServiceUrls:Notification"] = "https://notification.example.test",
    };

    private static string StartupFailure(string environment, Dictionary<string, string?> settings)
    {
        using var factory = new ConfiguredGameplayFactory(environment, settings);
        try
        {
            factory.CreateClient().Dispose();
        }
        catch (Exception thrown)
        {
            return StartupMessages.Flatten(thrown);
        }

        throw new Xunit.Sdk.XunitException("The host started although a required setting was invalid.");
    }

    private sealed class ConfiguredGameplayFactory(string environment, IReadOnlyDictionary<string, string?> settings)
        : WebApplicationFactory<QuizArena.Gameplay.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseContentRoot(AppContext.BaseDirectory);

            // Host-level settings: Program reads the connection string and CORS origins while building.
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            // The outbox worker would poll a database these hosts never create.
            builder.ConfigureTestServices(services =>
            {
                var worker = services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(DeliveryWorker));
                services.Remove(worker);
            });
        }
    }
}

/// <summary>Collects every message from a startup failure, including wrapped and aggregated ones.</summary>
internal static class StartupMessages
{
    public static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        var pending = new Stack<Exception>([exception]);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            messages.Add(current.Message);
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }

        return string.Join(Environment.NewLine, messages);
    }
}
