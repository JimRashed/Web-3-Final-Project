using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizArena.Data.Context;
using QuizArena.Domain.Lobby.Entities;
using QuizArena.Domain.QuestionBank.Entities;
using Xunit;

namespace QuizArena.IntegrationTests.Infrastructure;

public sealed class QuizArenaWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"QuizArenaTests_{Guid.NewGuid():N}";
    private readonly SemaphoreSlim _databaseLock = new(1, 1);
    public string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
    public string TestJoinCode => "TEST-LOBBY";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:Key", "integration-test-key-that-is-long-enough");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<QuizArenaContext>>();
            services.AddDbContext<QuizArenaContext>(options => options.UseSqlServer(ConnectionString));
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await _databaseLock.WaitAsync();
        try
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuizArenaContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            var category = new Category { Id = Guid.NewGuid(), Name = "General" };
            context.Categories.Add(category);
            context.Questions.Add(new Question { Id = Guid.NewGuid(), Prompt = "Test question", CorrectAnswer = "A", CategoryId = category.Id, Category = category });
            context.ActiveLobbies.Add(new ActiveLobby { Id = Guid.NewGuid(), JoinCode = TestJoinCode, HostId = "host", Players = new List<LobbyPlayer> { new() { Id = Guid.NewGuid(), PlayerId = "host" } } });
            await context.SaveChangesAsync();
        }
        finally
        {
            _databaseLock.Release();
        }
    }

    public Task InitializeDatabaseAsync() => ResetDatabaseAsync();

    public async Task DeleteDatabaseAsync()
    {
        await _databaseLock.WaitAsync();
        try
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuizArenaContext>();
            await context.Database.EnsureDeletedAsync();
        }
        finally
        {
            _databaseLock.Release();
        }
    }

    public Task InitializeAsync() => ResetDatabaseAsync();

    public new async Task DisposeAsync()
    {
        await DeleteDatabaseAsync();
        await base.DisposeAsync();
        _databaseLock.Dispose();
    }
}
