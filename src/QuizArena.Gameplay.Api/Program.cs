using QuizArena.Gameplay.Api.Errors;
using QuizArena.Gameplay.Api.Security;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Api.Services.Outbox;
using QuizArena.Gameplay.Data;
using QuizArena.Gameplay.Data.Outbox;
using QuizArena.Gameplay.Data.Repositories;
using QuizArena.Gameplay.Domain.Repositories;

namespace QuizArena.Gameplay.Api;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddProblemDetails();
        services.AddExceptionHandler<GameplayExceptionHandler>();
        services.AddControllers();
        services.AddQuizArenaSecurity(configuration);
        services.AddGameplayPersistence(configuration.GetConnectionString("DefaultConnection"));

        services.Configure<GameplayServiceOptions>(configuration.GetSection(GameplayServiceOptions.SectionName));
        services.Configure<MachineTokenOptions>(configuration.GetSection(MachineTokenOptions.SectionName));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IGameplayApplication, GameplayApplication>();

        // Machine-token client shared by every outbound call; it caches one token per scope.
        services.AddHttpClient(MachineTokenOptions.HttpClientName);
        services.AddSingleton<IMachineTokenSource, KeycloakMachineTokenSource>();

        // Typed adapters for the two ports the application service depends on.
        services.AddHttpClient<ILobbyGateway, LobbyHttpGateway>();
        services.AddHttpClient<IQuestionCatalog, QuestionBankHttpClient>();

        // Outbox delivery: one channel per destination, all driven by the background worker.
        services.AddHttpClient(HttpDeliveryChannel.HttpClientName);
        services.AddSingleton<IDeliveryChannel, StatisticsResultChannel>();
        services.AddSingleton<IDeliveryChannel, NotificationEventChannel>();
        services.AddHostedService<DeliveryWorker>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseQuizArenaSecurity();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .AllowAnonymous();
        app.MapControllers();

        if (args is ["--redrive", var rawDeliveryId])
        {
            await RedriveAsync(app, rawDeliveryId);
            return;
        }

        await app.RunAsync();
    }

    /// <summary>
    /// Operator redrive. A failed row is put back in the queue with a fresh attempt series while its
    /// payload, identifiers and history stay untouched. There is deliberately no HTTP admin route.
    /// </summary>
    private static async Task RedriveAsync(WebApplication app, string rawDeliveryId)
    {
        if (!Guid.TryParse(rawDeliveryId, out var deliveryId))
        {
            Console.Error.WriteLine("--redrive expects the delivery id (a GUID).");
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var requeued = await store.RedriveAsync(deliveryId, DateTime.UtcNow, CancellationToken.None);

        Console.WriteLine(requeued
            ? $"Delivery {deliveryId} is pending again."
            : $"Delivery {deliveryId} is not in a failed state (or does not exist).");
    }
}
