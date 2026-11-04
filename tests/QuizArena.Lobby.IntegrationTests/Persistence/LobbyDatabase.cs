using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuizArena.Lobby.Data.Context;
using QuizArena.Lobby.Data.Repositories;

namespace QuizArena.Lobby.IntegrationTests.Persistence;

internal sealed class LobbyDatabase : IAsyncDisposable
{
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private readonly string _connectionString;
    internal string ConnectionString => _connectionString;

    private LobbyDatabase(string scenario)
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaLobby_{scenario}_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
    }

    public static async Task<LobbyDatabase> CreateAsync(string scenario)
    {
        var database = new LobbyDatabase(scenario);
        try
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public LobbyDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<LobbyDbContext>().UseSqlServer(_connectionString);
        options.AddInterceptors(interceptors);
        return new LobbyDbContext(options.Options);
    }

    public TestContextFactory Factory(params IInterceptor[] interceptors) => new(this, interceptors);

    public LobbyRepository Repository(IJoinCodeSource? codes = null, params IInterceptor[] interceptors) =>
        new(Factory(interceptors), codes ?? new RandomJoinCodeSource());

    public async ValueTask DisposeAsync()
    {
        // This helper always targets its unique test catalog, never the server setting's catalog.
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    internal sealed class TestContextFactory(LobbyDatabase database, IInterceptor[] interceptors) : IDbContextFactory<LobbyDbContext>
    {
        public int TrackedEntities { get; private set; }

        public LobbyDbContext CreateDbContext()
        {
            var context = database.CreateContext(interceptors);
            context.ChangeTracker.Tracked += (_, _) => TrackedEntities++;
            return context;
        }

        public Task<LobbyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}

internal sealed class SequenceCodeSource(params string[] candidates) : IJoinCodeSource
{
    public int Calls { get; private set; }

    public string Next()
    {
        var candidate = candidates[Math.Min(Calls, candidates.Length - 1)];
        Calls++;
        return candidate;
    }
}
