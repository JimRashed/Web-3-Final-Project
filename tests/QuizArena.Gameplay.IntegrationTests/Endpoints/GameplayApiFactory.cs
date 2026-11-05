using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Repositories;
using QuizArena.Gameplay.IntegrationTests.Security;

namespace QuizArena.Gameplay.IntegrationTests.Endpoints;

/// <summary>
/// Runs the real Gameplay API pipeline (routing, JWT bearer validation, authorization policies,
/// exception mapping) against in-memory ports. The two remote services are replaced at the port
/// boundary, which is exactly where the HTTP adapters sit, so no test depends on Lobby or
/// QuestionBank being up.
/// </summary>
public sealed class GameplayApiFactory : WebApplicationFactory<QuizArena.Gameplay.Api.Program>
{
    public FakeMatchRepository Matches { get; } = new();

    public FakeLobbyGateway Lobby { get; } = new();

    public FakeQuestionCatalog Catalog { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // The runner hosts these assemblies outside the test project's output directory, so neither
        // the content root nor MvcTestingAppManifest.json resolves. Pin the content root and supply
        // the settings the host validates at startup.
        builder.UseContentRoot(AppContext.BaseDirectory);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = TestTokens.Issuer,
            ["Authentication:Audience"] = TestTokens.Audience,
            ["Authentication:BrowserClientId"] = TestTokens.BrowserClient,
            ["Authentication:MachineClientId"] = TestTokens.MachineClient,
            ["Authentication:RequireHttpsMetadata"] = "false",
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaGameplayEndpointTests;Trusted_Connection=True;TrustServerCertificate=True",
            ["ServiceUrls:Lobby"] = "http://lobby.invalid",
            ["ServiceUrls:QuestionBank"] = "http://questionbank.invalid",
            ["ServiceUrls:Statistics"] = "http://statistics.invalid",
            ["ServiceUrls:Notification"] = "http://notification.invalid",
        }));

        builder.ConfigureTestServices(services =>
        {
            // The outbox worker would poll a database this host does not use.
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IMatchRepository>();
            services.AddSingleton<IMatchRepository>(Matches);

            services.RemoveAll<ILobbyGateway>();
            services.AddSingleton<ILobbyGateway>(Lobby);

            services.RemoveAll<IQuestionCatalog>();
            services.AddSingleton<IQuestionCatalog>(Catalog);

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var metadata = new OpenIdConnectConfiguration { Issuer = TestTokens.Issuer };
                metadata.SigningKeys.Add(TestTokens.SigningKey);
                options.Configuration = metadata;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
            });
        });
    }

    public HttpClient Client(string? token)
    {
        var client = CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}

/// <summary>Turns 401/403 challenges into the ProblemDetails body the API produces in production.</summary>
public static class GameplayHttp
{
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request, cancellationToken);
    }

    public static async Task<JsonDocument> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}

/// <summary>In-memory <see cref="IMatchRepository"/> that copies on read and on save, like a database.</summary>
public sealed class FakeMatchRepository : IMatchRepository
{
    private readonly Dictionary<string, Match> _byId = [];

    public List<Match> Saved { get; } = [];

    public List<MatchDelivery> Enqueued { get; } = [];

    public Task<Match?> FindAsync(string matchId, CancellationToken cancellationToken) =>
        Task.FromResult(_byId.TryGetValue(matchId, out var stored) ? Copy(stored) : null);

    public Task<Match?> FindByLobbyAsync(string lobbyId, CancellationToken cancellationToken) =>
        Task.FromResult(_byId.Values.Where(match => match.LobbyId == lobbyId).Select(Copy).SingleOrDefault());

    public Task SaveAsync(Match match, IReadOnlyList<MatchDelivery> deliveries, CancellationToken cancellationToken)
    {
        _byId[match.MatchId] = Copy(match);
        Saved.Add(match);
        Enqueued.AddRange(deliveries);
        return Task.CompletedTask;
    }

    private static Match Copy(Match match) => Match.Rehydrate(
        match.MatchId, match.LobbyId, match.PlayerAId, match.PlayerBId, match.Status,
        match.PlayerAScore, match.PlayerBScore, match.CurrentQuestionIndex, match.WinnerUserId,
        match.Draw, match.CreatedAt, match.CompletedAt, match.Questions, match.Answers.ToArray());
}

public sealed class FakeLobbyGateway : ILobbyGateway
{
    public LobbyState? State { get; set; }

    public Exception? Failure { get; set; }

    public Task<LobbyState?> GetAsync(string joinCode, CancellationToken cancellationToken) =>
        Failure is not null
            ? Task.FromException<LobbyState?>(Failure)
            : Task.FromResult(string.Equals(joinCode, "ABC123", StringComparison.Ordinal) ? State : null);
}

public sealed class FakeQuestionCatalog : IQuestionCatalog
{
    public int Count { get; set; } = Match.RequiredQuestionCount;

    public Exception? Failure { get; set; }

    public Task<IReadOnlyList<QuestionSnapshot>> SnapshotAsync(int count, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromException<IReadOnlyList<QuestionSnapshot>>(Failure);
        }

        // Mirrors the real adapter: a catalog that cannot supply the exact count fails closed.
        if (Count != count)
        {
            return Task.FromException<IReadOnlyList<QuestionSnapshot>>(
                new GameplayRequestException(503, "The question bank cannot supply the requested set."));
        }

        IReadOnlyList<QuestionSnapshot> snapshots = Enumerable.Range(1, Count)
            .Select(index => QuestionSnapshot.Create(Guid.NewGuid(), $"Question {index}", ["A", "B", "C", "D"], "A"))
            .ToArray();

        return Task.FromResult(snapshots);
    }
}
