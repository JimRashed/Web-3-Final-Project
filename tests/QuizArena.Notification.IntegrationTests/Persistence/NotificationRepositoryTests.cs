using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Notification.Data.Context;
using QuizArena.Notification.Data.Repositories;
using QuizArena.Notification.Domain.Entities;
using QuizArena.Notification.Domain.Exceptions;
using Xunit;

namespace QuizArena.Notification.IntegrationTests.Persistence;

/// <summary>
/// Relational tests against a real SQL Server database created (and dropped) per test.
/// Set QUIZARENA_TEST_SQLSERVER to use a server other than LocalDB.
/// </summary>
public sealed class NotificationRepositoryTests : IAsyncLifetime
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly DbContextOptions<NotificationDbContext> _options;

    public NotificationRepositoryTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaNotification_Repo_{Guid.NewGuid():N}",
        };
        _options = new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer(connection.ConnectionString).Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task AddAsync_WithNewEvent_PersistsEventReadableFromFreshContext()
    {
        // Arrange
        var created = Event.CreateMatchCreated(Guid.NewGuid(), Issuer, "player-a", "match-1", Now);

        // Act
        await using (var context = NewContext())
        {
            await new EventRepository(context).AddAsync(created, CancellationToken.None);
        }

        // Assert
        await using var fresh = NewContext();
        var stored = await new EventRepository(fresh).FindAsync(created.EventId, CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.MatchId.Should().Be("match-1");
        stored.UserId.Should().Be("player-a");
        stored.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task AddAsync_WithIdenticalReplay_ReturnsStoredEventAndKeepsOriginalTimestamp()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var original = Event.CreateMatchCompleted(eventId, Issuer, "player-a", "match-1", "player-a", false, Now);
        var replay = Event.CreateMatchCompleted(eventId, Issuer, "player-a", "match-1", "player-a", false, Now.AddHours(1));

        // Act
        await using var context = NewContext();
        var repository = new EventRepository(context);
        await repository.AddAsync(original, CancellationToken.None);
        var resolved = await repository.AddAsync(replay, CancellationToken.None);

        // Assert
        resolved.EventId.Should().Be(eventId);
        resolved.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task AddAsync_WithChangedPayload_ThrowsConflict()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        await using var context = NewContext();
        var repository = new EventRepository(context);
        await repository.AddAsync(Event.CreateMatchCreated(eventId, Issuer, "player-a", "match-1", Now), CancellationToken.None);

        // Act
        var act = () => repository.AddAsync(
            Event.CreateMatchCreated(eventId, Issuer, "player-b", "match-1", Now),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EventConflictException>();
    }

    [Fact]
    public async Task GetByUserAsync_ReturnsOnlyOwnEventsNewestFirst()
    {
        // Arrange
        await using var context = NewContext();
        var repository = new EventRepository(context);
        await repository.AddAsync(Event.CreateMatchCreated(Guid.NewGuid(), Issuer, "player-a", "match-1", Now.AddMinutes(-5)), CancellationToken.None);
        await repository.AddAsync(Event.CreateMatchCreated(Guid.NewGuid(), Issuer, "player-a", "match-2", Now), CancellationToken.None);
        await repository.AddAsync(Event.CreateMatchCreated(Guid.NewGuid(), Issuer, "player-b", "match-3", Now), CancellationToken.None);

        // Act
        var events = await repository.GetByUserAsync(Issuer, "player-a", 1, 20, CancellationToken.None);

        // Assert
        events.Should().HaveCount(2);
        events[0].MatchId.Should().Be("match-2");
        events[1].MatchId.Should().Be("match-1");
    }

    [Fact]
    public async Task GetByUserAsync_PagesNewestFirstWithEventIdTieBreakAndEmptyBeyondTheEnd()
    {
        // Arrange: five events; two share a timestamp so the event id decides their order.
        await using var context = NewContext();
        var repository = new EventRepository(context);
        var tieLow = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var tieHigh = Guid.Parse("f0000000-0000-0000-0000-000000000000");
        await repository.AddAsync(Event.CreateMatchCreated(tieHigh, Issuer, "player-a", "tie-high", Now), CancellationToken.None);
        await repository.AddAsync(Event.CreateMatchCreated(tieLow, Issuer, "player-a", "tie-low", Now), CancellationToken.None);
        for (var minutes = 1; minutes <= 3; minutes++)
        {
            await repository.AddAsync(Event.CreateMatchCreated(Guid.NewGuid(), Issuer, "player-a", $"older-{minutes}", Now.AddMinutes(-minutes)), CancellationToken.None);
        }

        // Act
        var first = await repository.GetByUserAsync(Issuer, "player-a", 1, 2, CancellationToken.None);
        var second = await repository.GetByUserAsync(Issuer, "player-a", 2, 2, CancellationToken.None);
        var third = await repository.GetByUserAsync(Issuer, "player-a", 3, 2, CancellationToken.None);
        var beyond = await repository.GetByUserAsync(Issuer, "player-a", 4, 2, CancellationToken.None);

        // Assert
        first.Select(e => e.MatchId).Should().Equal("tie-low", "tie-high");
        second.Select(e => e.MatchId).Should().Equal("older-1", "older-2");
        third.Select(e => e.MatchId).Should().Equal("older-3");
        beyond.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetByUserAsync_WithPagingOutOfBounds_ThrowsBeforeQuerying(int page, int pageSize)
    {
        // Arrange
        await using var context = NewContext();
        var repository = new EventRepository(context);

        // Act
        var act = () => repository.GetByUserAsync(Issuer, "player-a", page, pageSize, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidPagingException>();
    }

    [Fact]
    public async Task UpsertAsync_WhenAbsent_InsertsAndWhenPresent_Updates()
    {
        // Arrange — separate contexts, so the insert and the update are independent requests
        // and the first result cannot be mutated by the second call through the change tracker.
        await using (var insertContext = NewContext())
        {
            var inserted = await new UserPreferenceRepository(insertContext)
                .UpsertAsync(UserPreference.CreateDefault(Issuer, "player-a"), CancellationToken.None);
            inserted.EmailEnabled.Should().BeFalse();
            inserted.PushEnabled.Should().BeFalse();
        }

        await using (var updateContext = NewContext())
        {
            var updated = await new UserPreferenceRepository(updateContext)
                .UpsertAsync(UserPreference.Rehydrate(Issuer, "player-a", true, true), CancellationToken.None);
            updated.EmailEnabled.Should().BeTrue();
            updated.PushEnabled.Should().BeTrue();
        }

        // Assert — a fresh read proves the values were actually persisted.
        await using var fresh = NewContext();
        var stored = await new UserPreferenceRepository(fresh).FindAsync(Issuer, "player-a", CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.EmailEnabled.Should().BeTrue();
        stored.PushEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task FindAsync_WhenSubjectUnknown_ReturnsNull()
    {
        // Arrange
        await using var context = NewContext();
        var repository = new UserPreferenceRepository(context);

        // Act
        var stored = await repository.FindAsync(Issuer, "nobody", CancellationToken.None);

        // Assert
        stored.Should().BeNull();
    }

    private NotificationDbContext NewContext() => new(_options);
}
