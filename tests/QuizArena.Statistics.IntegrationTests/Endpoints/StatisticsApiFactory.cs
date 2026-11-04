using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using QuizArena.Statistics.Data.Context;
using QuizArena.Statistics.IntegrationTests.Security;
using Xunit;

namespace QuizArena.Statistics.IntegrationTests.Endpoints;

/// <summary>
/// The real Statistics API (Development) with local OIDC metadata and its own migrated SQL Server database,
/// created once per test class and dropped afterwards. Set QUIZARENA_TEST_SQLSERVER to use another server.
/// </summary>
public sealed class StatisticsApiFactory : WebApplicationFactory<QuizArena.Statistics.Api.Program>, IAsyncLifetime
{
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    public StatisticsApiFactory()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        ConnectionString = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaStatistics_Http_{Guid.NewGuid():N}",
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    public async Task InitializeAsync()
    {
        await using var context = NewDbContext();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await using (var context = NewDbContext())
        {
            await context.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }

    public StatisticsDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<StatisticsDbContext>().UseSqlServer(ConnectionString).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // Host-level setting: Program reads the connection string while building, before app-configuration overrides apply.
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var metadata = new OpenIdConnectConfiguration { Issuer = TestTokens.Issuer };
                metadata.SigningKeys.Add(TestTokens.SigningKey);
                options.Configuration = metadata;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
            }));
    }
}
