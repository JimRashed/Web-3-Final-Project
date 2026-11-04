using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Lobby.Data.Context;
using QuizArena.Lobby.Api.Security;
using QuizArena.Lobby.Domain.Entities;
using QuizArena.Lobby.IntegrationTests.Persistence;
using QuizArena.Lobby.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Lobby.IntegrationTests.Endpoints;

public sealed class LobbyApiFixture : IAsyncLifetime
{
    private SecuredApiFactory? _factory;
    internal LobbyDatabase Database { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Database = await LobbyDatabase.CreateAsync("Endpoints");
        try
        {
            _factory = new SecuredApiFactory(Database.ConnectionString);
            Client = _factory.CreateClient();
            await using var context = await _factory.Services.GetRequiredService<IDbContextFactory<LobbyDbContext>>().CreateDbContextAsync();
            var expected = new SqlConnectionStringBuilder(Database.ConnectionString);
            var actual = context.Database.GetDbConnection();
            if (!string.Equals(actual.Database, expected.InitialCatalog, StringComparison.Ordinal)
                || !string.Equals(actual.DataSource, expected.DataSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Endpoint tests must target their disposable Lobby database.");
            }
        }
        catch
        {
            await Database.DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await Database.DisposeAsync();
    }

    internal async Task<ActiveLobby> SeedAsync(bool full = false)
    {
        var lobby = await Database.Repository().CreateAsync("host");
        return full ? (await Database.Repository().JoinAsync(lobby.JoinCode, "guest"))! : lobby;
    }

    internal static string Token(string scope = LobbyPolicies.LobbiesWrite, string subject = "host", ActorKind actor = ActorKind.User) =>
        TestTokens.Create(actor, scope, claims => claims["sub"] = subject);

    internal static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token, string? body = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request, cancellationToken);
    }

    internal static async Task AssertProblemAsync(HttpResponseMessage response, int status)
    {
        ((int)response.StatusCode).Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("type", "title", "status", "detail", "traceId");
        json.RootElement.GetProperty("status").GetInt32().Should().Be(status);
        Uri.IsWellFormedUriString(json.RootElement.GetProperty("type").GetString(), UriKind.Absolute).Should().BeTrue();
        var traceId = json.RootElement.GetProperty("traceId").GetString();
        traceId.Should().NotBeNullOrWhiteSpace();
        traceId!.Length.Should().BeInRange(1, 100);
        json.RootElement.GetProperty("detail").GetString().Should().Be(json.RootElement.GetProperty("title").GetString());
    }
}
