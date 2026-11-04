using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Identity.Data.Context;
using QuizArena.Identity.Data.Repositories;
using QuizArena.Identity.Domain.Entities;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Persistence;

/// <summary>
/// Relational tests against a real SQL Server database created (and dropped) per test.
/// Set QUIZARENA_TEST_SQLSERVER to use a server other than LocalDB.
/// </summary>
public sealed class PlayerProfileRepositoryTests : IAsyncLifetime
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly DbContextOptions<IdentityDbContext> _options;

    public PlayerProfileRepositoryTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaIdentity_Repo_{Guid.NewGuid():N}",
        };
        _options = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(connection.ConnectionString).Options;
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
    public async Task AddIfAbsentAsync_WithNewIdentity_PersistsProfileReadableFromFreshContext()
    {
        // Arrange
        var profile = PlayerProfile.Create(Issuer, "player-a", "Alex", Now);

        // Act
        await using (var context = NewContext())
        {
            await new PlayerProfileRepository(context).AddIfAbsentAsync(profile, CancellationToken.None);
        }

        // Assert
        await using var fresh = NewContext();
        var stored = await new PlayerProfileRepository(fresh).FindByIdentityAsync(Issuer, "player-a", CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.Id.Should().Be(profile.Id);
        stored.DisplayName.Should().Be("Alex");
        stored.CreatedAt.Should().Be(Now);
        stored.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        stored.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task AddIfAbsentAsync_WhenProfileExists_ReturnsStoredProfileWithoutDuplicate()
    {
        // Arrange
        var first = PlayerProfile.Create(Issuer, "player-a", "Alex", Now);
        var second = PlayerProfile.Create(Issuer, "player-a", "Someone else", Now.AddMinutes(1));
        await using (var context = NewContext())
        {
            await new PlayerProfileRepository(context).AddIfAbsentAsync(first, CancellationToken.None);
        }

        // Act
        PlayerProfile result;
        await using (var context = NewContext())
        {
            result = await new PlayerProfileRepository(context).AddIfAbsentAsync(second, CancellationToken.None);
        }

        // Assert
        result.Id.Should().Be(first.Id);
        result.DisplayName.Should().Be("Alex");
        await using var fresh = NewContext();
        (await fresh.UserProfiles.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AddIfAbsentAsync_WithConcurrentFirstRequests_CreatesExactlyOneProfile()
    {
        // Arrange: eight requests for the same new identity, each with its own context and candidate id.
        var candidates = Enumerable.Range(0, 8)
            .Select(i => PlayerProfile.Create(Issuer, "player-a", $"Alex {i}", Now))
            .ToList();

        // Act
        var results = await Task.WhenAll(candidates.Select(async candidate =>
        {
            await using var context = NewContext();
            return await new PlayerProfileRepository(context).AddIfAbsentAsync(candidate, CancellationToken.None);
        }));

        // Assert
        results.Select(r => r.Id).Distinct().Should().ContainSingle();
        await using var fresh = NewContext();
        (await fresh.UserProfiles.CountAsync()).Should().Be(1);
        (await fresh.UserProfiles.SingleAsync()).Id.Should().Be(results[0].Id);
    }

    [Fact]
    public async Task FindByIdentityAsync_ComparesSubjectCaseSensitively()
    {
        // Arrange
        await using (var context = NewContext())
        {
            var repository = new PlayerProfileRepository(context);
            await repository.AddIfAbsentAsync(PlayerProfile.Create(Issuer, "abc", "Lower", Now), CancellationToken.None);
            await repository.AddIfAbsentAsync(PlayerProfile.Create(Issuer, "ABC", "Upper", Now), CancellationToken.None);
        }

        // Act
        await using var fresh = NewContext();
        var repositoryUnderTest = new PlayerProfileRepository(fresh);
        var lower = await repositoryUnderTest.FindByIdentityAsync(Issuer, "abc", CancellationToken.None);
        var upper = await repositoryUnderTest.FindByIdentityAsync(Issuer, "ABC", CancellationToken.None);
        var mixed = await repositoryUnderTest.FindByIdentityAsync(Issuer, "Abc", CancellationToken.None);

        // Assert
        lower!.DisplayName.Should().Be("Lower");
        upper!.DisplayName.Should().Be("Upper");
        mixed.Should().BeNull();
        (await fresh.UserProfiles.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task UniqueIndex_WithDuplicateIssuerAndSubject_IsEnforcedByDatabase()
    {
        // Arrange
        await using var context = NewContext();
        context.UserProfiles.Add(PlayerProfile.Create(Issuer, "player-a", "One", Now));
        context.UserProfiles.Add(PlayerProfile.Create(Issuer, "player-a", "Two", Now));

        // Act
        var act = () => context.SaveChangesAsync();

        // Assert
        (await act.Should().ThrowAsync<DbUpdateException>()).WithInnerException<SqlException>();
    }

    [Fact]
    public async Task FindByIdentityAsync_DoesNotTrackResult()
    {
        // Arrange
        await using (var context = NewContext())
        {
            await new PlayerProfileRepository(context).AddIfAbsentAsync(PlayerProfile.Create(Issuer, "player-a", "Alex", Now), CancellationToken.None);
        }

        // Act
        await using var fresh = NewContext();
        await new PlayerProfileRepository(fresh).FindByIdentityAsync(Issuer, "player-a", CancellationToken.None);

        // Assert
        fresh.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task RenameAsync_WithNewName_PersistsNameAndTimestamp()
    {
        // Arrange
        var profile = PlayerProfile.Create(Issuer, "player-a", "Alex", Now);
        await using (var context = NewContext())
        {
            await new PlayerProfileRepository(context).AddIfAbsentAsync(profile, CancellationToken.None);
        }

        // Act
        await using (var context = NewContext())
        {
            var renamed = await new PlayerProfileRepository(context).RenameAsync(Issuer, "player-a", " Sam ", Now.AddHours(1), CancellationToken.None);
            renamed!.DisplayName.Should().Be("Sam");
        }

        // Assert
        await using var fresh = NewContext();
        var stored = await new PlayerProfileRepository(fresh).FindByIdentityAsync(Issuer, "player-a", CancellationToken.None);
        stored!.DisplayName.Should().Be("Sam");
        stored.UpdatedAt.Should().Be(Now.AddHours(1));
        stored.CreatedAt.Should().Be(Now);
        stored.Id.Should().Be(profile.Id);
    }

    [Fact]
    public async Task RenameAsync_WithSameName_LeavesUpdatedAtUnchanged()
    {
        // Arrange
        await using (var context = NewContext())
        {
            await new PlayerProfileRepository(context).AddIfAbsentAsync(PlayerProfile.Create(Issuer, "player-a", "Alex", Now), CancellationToken.None);
        }

        // Act
        await using (var context = NewContext())
        {
            await new PlayerProfileRepository(context).RenameAsync(Issuer, "player-a", "Alex", Now.AddHours(1), CancellationToken.None);
        }

        // Assert
        await using var fresh = NewContext();
        (await fresh.UserProfiles.SingleAsync()).UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task RenameAsync_WithoutProfile_ReturnsNull()
    {
        // Act
        await using var context = NewContext();
        var result = await new PlayerProfileRepository(context).RenameAsync(Issuer, "nobody", "Sam", Now, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindByIdentityAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await using var context = NewContext();

        // Act
        var act = () => new PlayerProfileRepository(context).FindByIdentityAsync(Issuer, "player-a", cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private IdentityDbContext NewContext() => new(_options);
}
