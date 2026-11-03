using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace QuizArena.Notification.Api.Security;

public static class SecurityExtensions
{
    public const string FrontendCorsPolicy = "frontend";

    public static IServiceCollection AddQuizArenaSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<QuizArenaAuthenticationOptions>()
            .Bind(configuration.GetSection(QuizArenaAuthenticationOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.Authority, UriKind.Absolute, out _), "Authentication:Authority must be an absolute URI.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Authentication:Audience is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BrowserClientId), "Authentication:BrowserClientId is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.MachineClientId), "Authentication:MachineClientId is required.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<QuizArenaAuthenticationOptions>>((jwt, auth) => ConfigureJwtBearer(jwt, auth.Value));

        services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();

            foreach (var (scope, actor) in NotificationPolicies.All)
            {
                options.AddPolicy(scope, policy => policy
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .AddRequirements(new ScopeRequirement(scope, actor)));
            }
        });

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type")));

        return services;
    }

    public static WebApplication UseQuizArenaSecurity(this WebApplication app)
    {
        app.UseCors(FrontendCorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions jwt, QuizArenaAuthenticationOptions settings)
    {
        jwt.Authority = settings.Authority;
        jwt.Audience = settings.Audience;
        jwt.RequireHttpsMetadata = settings.RequireHttpsMetadata;
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // Keycloak's iss has no trailing slash; tolerate one in configuration.
            ValidIssuer = settings.Authority.TrimEnd('/'),
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = QuizArenaClaims.Subject,
        };
        jwt.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // Keycloak marks access tokens typ=Bearer; ID and refresh tokens are rejected.
                if (!string.Equals(context.Principal?.FindFirst(QuizArenaClaims.TokenType)?.Value, QuizArenaClaims.AccessTokenType, StringComparison.Ordinal))
                {
                    context.Fail("Only access tokens are accepted.");
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                // RFC 6750: tell clients whether the token was missing or rejected.
                context.Response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
                    ? "Bearer"
                    : "Bearer error=\"invalid_token\"";
                await WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Missing or invalid bearer token");
            },
            OnForbidden = context =>
                WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Insufficient scope, actor kind or ownership"),
        };
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, int status, string title)
    {
        httpContext.Response.StatusCode = status;
        var problems = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = status, Title = title, Detail = title },
        });
    }
}
