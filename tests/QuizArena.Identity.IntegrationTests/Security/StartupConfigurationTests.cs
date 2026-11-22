using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Security;

/// <summary>
/// SEC-A2 (#138): Identity stops at startup when its database (which also stores profile pictures,
/// SEC-05) or its token authority is missing or invalid. Failures name the key, never the value.
/// The host is only started; no database is created or opened.
/// </summary>
public sealed class StartupConfigurationTests
{
    private static readonly string Sentinel = $"sentinel-{Guid.NewGuid():N}";

    [Fact]
    public async Task Startup_ProductionWithCompleteConfiguration_ServesHealth()
    {
        // Arrange
        using var factory = new ConfiguredIdentityFactory("Production", ValidProduction());
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Startup_WithoutConnectionString_FailsNamingTheKey(string environment)
    {
        // Arrange
        var settings = ValidProduction();
        settings["ConnectionStrings:DefaultConnection"] = " ";

        // Act
        var failure = StartupFailure(environment, settings);

        // Assert
        failure.Should().Contain("ConnectionStrings:DefaultConnection");
    }

    [Theory]
    [InlineData("Development", "{0}=value")]
    [InlineData("Production", "{0}=value")]
    [InlineData("Production", "Server=db;{0}")]
    [InlineData("Production", "Server=db;Connect Timeout={0}")]
    public void Startup_WithMalformedConnectionString_FailsNamingTheKeyOnly(string environment, string template)
    {
        // Arrange: unknown keyword, missing "=", and a non-numeric value. None of them opens a connection.
        var settings = ValidProduction();
        settings["ConnectionStrings:DefaultConnection"] = string.Format(System.Globalization.CultureInfo.InvariantCulture, template, Sentinel);

        // Act
        var failure = StartupFailure(environment, settings);

        // Assert
        failure.Should().Contain("ConnectionStrings:DefaultConnection").And.NotContain(Sentinel);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Startup_WithInvalidAuthority_FailsNamingTheKeyOnly(string environment)
    {
        // Arrange
        var settings = ValidProduction();
        settings["Authentication:Authority"] = Sentinel;

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

    private static Dictionary<string, string?> ValidProduction() => new(StringComparer.Ordinal)
    {
        ["Authentication:Authority"] = "https://idp.example.test/realms/quizarena",
        ["Authentication:RequireHttpsMetadata"] = "true",
        ["Cors:AllowedOrigins:0"] = "https://app.example.test",
        ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaIdentityStartupTests;Trusted_Connection=True;TrustServerCertificate=True",
    };

    private static string StartupFailure(string environment, Dictionary<string, string?> settings)
    {
        using var factory = new ConfiguredIdentityFactory(environment, settings);
        try
        {
            factory.CreateClient().Dispose();
        }
        catch (Exception thrown)
        {
            return Flatten(thrown);
        }

        throw new Xunit.Sdk.XunitException("The host started although a required setting was invalid.");
    }

    private static string Flatten(Exception exception)
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

    private sealed class ConfiguredIdentityFactory(string environment, IReadOnlyDictionary<string, string?> settings)
        : WebApplicationFactory<QuizArena.Identity.Api.Program>
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
        }
    }
}
