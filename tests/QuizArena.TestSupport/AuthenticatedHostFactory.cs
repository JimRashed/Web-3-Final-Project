using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace QuizArena.TestSupport;

/// <summary>
/// Host factory that runs the real API pipeline — including the real <c>JwtBearer</c> handler and
/// the production authorization policies — against the metadata published by
/// <see cref="EphemeralTokenIssuer"/>. Only the metadata source is replaced; nothing about token
/// validation or policy enforcement is stubbed out.
/// </summary>
public class AuthenticatedHostFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private readonly EphemeralTokenIssuer _tokens;
    private readonly Dictionary<string, string?> _configuration;

    public AuthenticatedHostFactory(EphemeralTokenIssuer tokens, IDictionary<string, string?>? configuration = null)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _configuration = configuration is null
            ? []
            : new Dictionary<string, string?>(configuration, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Settings applied on top of the API's own configuration (for example a connection string).</summary>
    protected IReadOnlyDictionary<string, string?> Configuration => _configuration;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            if (_configuration.Count > 0)
            {
                configuration.AddInMemoryCollection(_configuration);
            }
        });

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = _tokens.Metadata;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(_tokens.Metadata);
            });

            ConfigureServiceOverrides(services);
        });
    }

    /// <summary>Hook for service-specific test registrations (probe controllers, fakes, seeding).</summary>
    protected virtual void ConfigureServiceOverrides(IServiceCollection services)
    {
    }
}
