using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizArena.Notification.Api.Contracts;
using QuizArena.Notification.Api.Services;
using QuizArena.Notification.Data.Context;
using QuizArena.TestSupport;

namespace QuizArena.Notification.IntegrationTests.Endpoints;

/// <summary>
/// Runs the real Notification API (routing, bearer validation, policies, ProblemDetails) with a
/// stub application service by default, or the real application when given a SQL connection string.
/// </summary>
public sealed class NotificationApiFactory
    : AuthenticatedHostFactory<QuizArena.Notification.Api.Program>
{
    public const string Audience = "quizarena-notification";

    // One key per test process: the host validates with the same metadata the tests sign with.
    private static readonly EphemeralTokenIssuer SharedIssuer = new();
    private readonly string? _sqlConnectionString;

    public NotificationApiFactory()
        : this(null)
    {
    }

    internal NotificationApiFactory(string? sqlConnectionString)
        : base(
            SharedIssuer,
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    sqlConnectionString ?? "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaNotification_Endpoints;Trusted_Connection=True;TrustServerCertificate=True",
            })
    {
        _sqlConnectionString = sqlConnectionString;
    }

    public EphemeralTokenIssuer Tokens => SharedIssuer;

    public StubEventApplication Application { get; } = new();

    protected override void ConfigureServiceOverrides(IServiceCollection services)
    {
        if (_sqlConnectionString is null)
        {
            services.AddSingleton<IEventApplication>(Application);
        }
        else
        {
            // Program captures the connection string before app-configuration overrides apply.
            var options = new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer(_sqlConnectionString).Options;
            services.RemoveAll<NotificationDbContext>();
            services.RemoveAll<DbContextOptions<NotificationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<NotificationDbContext>>();
            services.AddScoped(_ => new NotificationDbContext(options));
        }
    }
}

/// <summary>Deterministic application service double; records what the endpoint passed in.</summary>
public sealed class StubEventApplication : IEventApplication
{
    public List<CreateEventRequest> Published { get; } = [];

    public List<string> Subjects { get; } = [];

    public Task<EventResponse> PublishAsync(CreateEventRequest request, string issuer, CancellationToken cancellationToken)
    {
        Published.Add(request);
        return Task.FromResult(new EventResponse(
            request.EventId,
            request.Type,
            request.UserId,
            new EventPayloadResponse(request.Payload.MatchId ?? string.Empty, request.Payload.WinnerUserId, request.Payload.Draw),
            DateTime.UtcNow));
    }

    public List<(int Page, int PageSize)> Pages { get; } = [];

    public Task<IReadOnlyList<EventResponse>> GetEventsAsync(string issuer, string subject, int page, int pageSize, CancellationToken cancellationToken)
    {
        Subjects.Add(subject);
        Pages.Add((page, pageSize));
        return Task.FromResult<IReadOnlyList<EventResponse>>([]);
    }

    public Task<UserPreferencesResponse> GetPreferencesAsync(string issuer, string subject, CancellationToken cancellationToken)
    {
        Subjects.Add(subject);
        return Task.FromResult(new UserPreferencesResponse(false, false));
    }

    public Task<UserPreferencesResponse> UpdatePreferencesAsync(string issuer, string subject, UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        Subjects.Add(subject);
        return Task.FromResult(new UserPreferencesResponse(request.EmailEnabled, request.PushEnabled));
    }
}
