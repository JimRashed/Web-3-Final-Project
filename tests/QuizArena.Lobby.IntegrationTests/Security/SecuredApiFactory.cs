using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Lobby.Data.Context;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace QuizArena.Lobby.IntegrationTests.Security;

/// <summary>Runs the real API pipeline with local OIDC metadata (issuer + ephemeral signing key) instead of Keycloak discovery.</summary>
public sealed class SecuredApiFactory : WebApplicationFactory<QuizArena.Lobby.Api.Program>
{
    private readonly string? _connectionString;

    public SecuredApiFactory() { }

    internal SecuredApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        if (_connectionString is not null)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = _connectionString }));
        }

        builder.ConfigureTestServices(services =>
        {
            if (_connectionString is not null)
            {
                // Program captures its configured connection during registration. Override only the
                // owned options so the actual repository factory uses this fixture's disposable catalog.
                services.RemoveAll<DbContextOptions<LobbyDbContext>>();
                services.AddSingleton(new DbContextOptionsBuilder<LobbyDbContext>().UseSqlServer(_connectionString).Options);
            }

            services.AddControllers().AddApplicationPart(typeof(SecurityProbeController).Assembly);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var metadata = new OpenIdConnectConfiguration { Issuer = TestTokens.Issuer };
                metadata.SigningKeys.Add(TestTokens.SigningKey);
                options.Configuration = metadata;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
            });
        });
    }
}
