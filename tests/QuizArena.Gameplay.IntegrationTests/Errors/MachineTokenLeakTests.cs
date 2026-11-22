using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.IntegrationTests.Endpoints;
using QuizArena.Gameplay.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Errors;

/// <summary>
/// SEC-A2 (#138): when the identity provider refuses the machine-token request, the caller gets a 503
/// problem body that carries neither the client secret nor the identity provider's response body.
/// Runs the real token client and the real Lobby adapter; only the token endpoint is stubbed.
/// </summary>
public sealed class MachineTokenLeakTests
{
    private const string HttpsIssuer = "https://idp.example.test/realms/quizarena";
    private const string IdpError = "invalid_client";

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Create_WhenTokenEndpointRefusesSecret_Returns503WithoutSecretOrIdpBody(string environment)
    {
        // Arrange
        var secret = $"sentinel-secret-{Guid.NewGuid():N}";
        using var baseFactory = new GameplayApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            // Program reads the connection string and CORS origins while building; Production has no appsettings value for them.
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaGameplayLeakTests;Trusted_Connection=True;TrustServerCertificate=True");
            builder.UseSetting("Cors:AllowedOrigins:0", "https://app.example.test");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A complete non-Development configuration, so the Production run passes startup validation.
                ["Authentication:Authority"] = HttpsIssuer,
                ["Authentication:RequireHttpsMetadata"] = "true",
                ["Authentication:MachineClientSecret"] = secret,
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILobbyGateway>();
                services.AddHttpClient<ILobbyGateway, LobbyHttpGateway>();
                services.AddHttpClient(MachineTokenOptions.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new RefusingTokenEndpoint(secret));
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestTokens.Create(
            ActorKind.User,
            GameplayPolicies.MatchesWrite,
            claims => claims["iss"] = HttpsIssuer));

        // Act
        using var response = await GameplayHttp.SendAsync(client, HttpMethod.Post, "/matches", new { joinCode = "ABC123" });
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body.Should().NotContain(secret).And.NotContain(IdpError);
        using var problem = JsonDocument.Parse(body);
        problem.RootElement.TryGetProperty("exception", out _).Should().BeFalse();
        problem.RootElement.TryGetProperty("stackTrace", out _).Should().BeFalse();
    }

    /// <summary>A token endpoint that refuses the client and echoes the submitted secret in its body.</summary>
    private sealed class RefusingTokenEndpoint(string secret) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent($$"""{"error":"{{IdpError}}","echo":"{{secret}}"}""", Encoding.UTF8, "application/json"),
            });
    }
}
