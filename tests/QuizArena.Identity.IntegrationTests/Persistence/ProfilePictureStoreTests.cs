using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuizArena.Identity.Data.Context;
using QuizArena.Identity.Data.Repositories;
using QuizArena.Identity.Domain.Entities;
using QuizArena.Identity.Domain.Exceptions;
using Xunit;

namespace QuizArena.Identity.IntegrationTests.Persistence;

/// <summary>
/// Picture persistence against a real SQL Server database created (and dropped) per test, including injected write,
/// commit and cancellation failures and concurrent replacements (SEC-06, SEC-07).
/// Set QUIZARENA_TEST_SQLSERVER to use a server other than LocalDB.
/// </summary>
public sealed class ProfilePictureStoreTests : IAsyncLifetime
{
    private const string Issuer = "http://localhost:8080/realms/quizarena";
    private const string DefaultServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] OldBytes = [0x89, 0x50, 0x4E, 0x47, 0x01];
    private static readonly byte[] NewBytes = [0xFF, 0xD8, 0xFF, 0x02];

    private readonly string _connectionString;
    private Guid _profileId;

    public ProfilePictureStoreTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        _connectionString = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"QuizArenaIdentity_Picture_{Guid.NewGuid():N}",
        }.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();
        var profile = await new PlayerProfileRepository(context)
            .AddIfAbsentAsync(PlayerProfile.Create(Issuer, "player-a", "Alex", Now.AddDays(-1)), CancellationToken.None);
        _profileId = profile.Id;
    }

    public async Task DisposeAsync()
    {
        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task ReplaceAsync_FirstUpload_PersistsVersionOneReadableFromFreshContext()
    {
        // Act
        ProfilePictureInfo info;
        await using (var context = NewContext())
        {
            info = await new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/png", OldBytes, Now, CancellationToken.None);
        }

        // Assert
        info.Should().Be(new ProfilePictureInfo("image/png", 1, Now));
        var stored = await ReadAsync();
        stored.Should().NotBeNull();
        stored!.Content.Should().Equal(OldBytes);
        stored.ContentType.Should().Be("image/png");
        stored.Version.Should().Be(1);
        stored.UpdatedAt.Should().Be(Now);
        stored.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ReplaceAsync_WhenPictureExists_ReplacesBytesAndIncrementsVersion()
    {
        // Arrange
        await SeedAsync();

        // Act
        ProfilePictureInfo info;
        await using (var context = NewContext())
        {
            info = await new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);
        }

        // Assert
        info.Should().Be(new ProfilePictureInfo("image/jpeg", 2, Now.AddMinutes(1)));
        var stored = await ReadAsync();
        stored!.Content.Should().Equal(NewBytes);
        stored.ContentType.Should().Be("image/jpeg");
        stored.Version.Should().Be(2);
    }

    [Fact]
    public async Task ReplaceAsync_SequentialReplacements_EachSucceedsVersionIncrementsByOne()
    {
        // Arrange
        var versions = new List<int>();

        // Act
        for (var i = 0; i < 3; i++)
        {
            await using var context = NewContext();
            var info = await new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/webp", [(byte)(i + 1)], Now.AddMinutes(i), CancellationToken.None);
            versions.Add(info.Version);
        }

        // Assert
        versions.Should().Equal(1, 2, 3);
        (await ReadAsync())!.Content.Should().Equal(3);
    }

    [Fact]
    public async Task GetInfoAsync_ReturnsReferenceWithoutBytes()
    {
        // Arrange
        await SeedAsync();
        var commands = new CommandRecorder();

        // Act
        ProfilePictureInfo? info;
        await using (var context = NewContext(commands))
        {
            info = await new ProfilePictureStore(context).GetInfoAsync(_profileId, CancellationToken.None);
        }

        // Assert
        info.Should().Be(new ProfilePictureInfo("image/png", 1, Now));
        commands.Texts.Should().ContainSingle().Which.Should().NotContain("[Content]");
    }

    [Fact]
    public async Task GetAsync_WhenNone_ReturnsNull()
    {
        // Act
        await using var context = NewContext();
        var store = new ProfilePictureStore(context);
        var picture = await store.GetAsync(_profileId, CancellationToken.None);
        var info = await store.GetInfoAsync(_profileId, CancellationToken.None);

        // Assert
        picture.Should().BeNull();
        info.Should().BeNull();
    }

    [Fact]
    public async Task ReplaceAsync_WhenObjectWriteFails_KeepsPreviousPicture()
    {
        // Arrange
        await SeedAsync();
        var failure = new FailingPictureWrite();

        // Act
        Func<Task> act;
        await using (var context = NewContext(failure))
        {
            act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);
            (await act.Should().ThrowAsync<DbUpdateException>()).WithInnerException<InvalidOperationException>().WithMessage(FailingPictureWrite.Message);
        }

        // Assert
        failure.Attempts.Should().Be(1);
        var stored = await ReadAsync();
        stored!.Content.Should().Equal(OldBytes);
        stored.Version.Should().Be(1);
        stored.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task ReplaceAsync_FirstUploadWhenObjectWriteFails_LeavesNoPicture()
    {
        // Arrange
        var failure = new FailingPictureWrite();

        // Act
        await using (var context = NewContext(failure))
        {
            var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/png", OldBytes, Now, CancellationToken.None);
            (await act.Should().ThrowAsync<DbUpdateException>()).WithInnerException<InvalidOperationException>();
        }

        // Assert
        failure.Attempts.Should().Be(1);
        (await ReadAsync()).Should().BeNull();
    }

    [Fact]
    public async Task ReplaceAsync_WhenCommitFails_RollsBackToPreviousPictureAndRetrySucceeds()
    {
        // Arrange
        await SeedAsync();
        var commit = new FailingCommit();
        await using var context = NewContext(commit);
        var store = new ProfilePictureStore(context);

        // Act
        var act = () => store.ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(FailingCommit.Message);
        var afterFailure = await ReadAsync();
        var retried = await store.ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(2), CancellationToken.None);

        // Assert
        commit.Failures.Should().Be(1);
        afterFailure!.Content.Should().Equal(OldBytes);
        afterFailure.Version.Should().Be(1);
        context.ChangeTracker.Entries().Should().ContainSingle("only the retried, committed picture is tracked");
        retried.Should().Be(new ProfilePictureInfo("image/jpeg", 2, Now.AddMinutes(2)));
        (await ReadAsync())!.Content.Should().Equal(NewBytes);
    }

    [Fact]
    public async Task ReplaceAsync_WhenCommitFailsAfterPictureWrite_RollsBackTheWrittenRow()
    {
        // Arrange
        await SeedAsync();
        var commands = new CommandRecorder();
        var commit = new FailingCommit();

        // Act
        await using (var context = NewContext(commands, commit))
        {
            var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(FailingCommit.Message);
        }

        // Assert
        commands.Texts.Should().Contain(text => text.Contains("UPDATE [ProfilePictures]", StringComparison.Ordinal), "the commit must come after the picture write inside the same transaction");
        var stored = await ReadAsync();
        stored!.Content.Should().Equal(OldBytes);
        stored.Version.Should().Be(1);
    }

    [Fact]
    public async Task ReplaceAsync_WhenCommitAcknowledgementIsLost_VerifiesCommitAndCountsTheUploadOnce()
    {
        // Arrange: the real commit succeeds, then the acknowledgement is lost (the commit step throws afterwards).
        await SeedAsync();
        var commands = new CommandRecorder();
        var lostAck = new LostCommitAcknowledgement();
        await using var context = NewContext(commands, lostAck);
        var store = new ProfilePictureStore(context);

        // Act
        var info = await store.ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);

        // Assert: the store verified the committed row and reported success, so the caller has nothing to retry.
        lostAck.Failures.Should().Be(1);
        info.Should().Be(new ProfilePictureInfo("image/jpeg", 2, Now.AddMinutes(1)));
        commands.Texts.Count(text => text.Contains("UPDATE [ProfilePictures]", StringComparison.Ordinal))
            .Should().Be(1, "one logical upload issues exactly one replacement write; the store never retries it");
        var stored = await ReadAsync();
        stored!.Version.Should().Be(2, "the version moved from the prior 1 to exactly 2");
        stored.Content.Should().Equal(NewBytes);
        stored.ContentType.Should().Be("image/jpeg");
        context.ChangeTracker.Entries<ProfilePicture>().Should().ContainSingle()
            .Which.State.Should().Be(EntityState.Unchanged, "the committed picture stays tracked in its stored state");
    }

    [Fact]
    public async Task ReplaceAsync_FirstUploadWhenCommitAcknowledgementIsLost_ReturnsVersionOne()
    {
        // Arrange
        var lostAck = new LostCommitAcknowledgement();

        // Act
        ProfilePictureInfo info;
        await using (var context = NewContext(lostAck))
        {
            info = await new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/png", OldBytes, Now, CancellationToken.None);
        }

        // Assert
        lostAck.Failures.Should().Be(1);
        info.Should().Be(new ProfilePictureInfo("image/png", 1, Now));
        var stored = await ReadAsync();
        stored!.Version.Should().Be(1);
        stored.Content.Should().Equal(OldBytes);
    }

    [Fact]
    public async Task ReplaceAsync_WhenAcknowledgementIsLostAndAnotherReplaceFollows_ThrowsConflict()
    {
        // Arrange: after this call commits, a competing replace commits before the outcome is verified.
        await SeedAsync();
        byte[] competingBytes = [0x77];
        var lostAck = new LostCommitAcknowledgement(async () =>
        {
            await using var other = NewContext();
            await new ProfilePictureStore(other).ReplaceAsync(_profileId, "image/webp", competingBytes, Now.AddMinutes(2), CancellationToken.None);
        });

        // Act
        await using (var context = NewContext(lostAck))
        {
            var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);
            (await act.Should().ThrowAsync<ProfilePictureConflictException>())
                .Which.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(LostCommitAcknowledgement.Message);
        }

        // Assert: the current row is not the write this call made, so the store cannot claim it.
        var stored = await ReadAsync();
        stored!.Version.Should().Be(3);
        stored.Content.Should().Equal(competingBytes);
    }

    [Fact]
    public async Task ReplaceAsync_WhenAcknowledgementIsLostAndProfileWasDeleted_ThrowsProfileNotFound()
    {
        // Arrange
        await SeedAsync();
        var lostAck = new LostCommitAcknowledgement(DeleteProfileAsync);

        // Act
        await using var context = NewContext(lostAck);
        var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<PlayerProfileNotFoundException>();
        (await ReadAsync()).Should().BeNull();
    }

    [Fact]
    public async Task ReplaceAsync_WhenProfileDeletedBeforeUpdate_ThrowsProfileNotFoundNotConflict()
    {
        // Arrange: the profile (and by cascade its picture) is deleted between the read and the UPDATE.
        await SeedAsync();
        await using var context = NewContext(new RunBeforeSave(DeleteProfileAsync));

        // Act
        var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<PlayerProfileNotFoundException>())
            .Which.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
        context.ChangeTracker.Entries<ProfilePicture>().Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_OnFailure_DetachesOnlyThePictureEntry()
    {
        // Arrange
        await SeedAsync();
        var failure = new FailingPictureWrite();
        await using var context = NewContext(failure);
        var profile = await context.UserProfiles.SingleAsync(p => p.Id == _profileId);

        // Act
        var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);
        await act.Should().ThrowAsync<DbUpdateException>();

        // Assert
        context.ChangeTracker.Entries<ProfilePicture>().Should().BeEmpty();
        context.Entry(profile).State.Should().Be(EntityState.Unchanged, "unrelated entries in the scoped context are kept");
    }

    [Theory]
    [InlineData("image/gif", 1, 3, true)]
    [InlineData("image/svg+xml", 1, 3, true)]
    [InlineData("image/png", 0, 3, true)]
    [InlineData("image/png", -4, 3, true)]
    [InlineData("image/png", 1, 0, false)]
    public async Task Reads_WithCorruptStoredRow_FailLoudly(string contentType, int version, int contentLength, bool infoDetects)
    {
        // Arrange: bypass the domain and write an invalid row directly.
        await InsertRawAsync(contentType, version, new byte[contentLength]);
        await using var context = NewContext();
        var store = new ProfilePictureStore(context);

        // Act
        var get = () => store.GetAsync(_profileId, CancellationToken.None);
        var getInfo = () => store.GetInfoAsync(_profileId, CancellationToken.None);

        // Assert
        (await get.Should().ThrowAsync<ProfilePictureCorruptException>()).Which.InnerException.Should().BeOfType<InvalidProfileException>();
        if (infoDetects)
        {
            (await getInfo.Should().ThrowAsync<ProfilePictureCorruptException>()).Which.InnerException.Should().BeOfType<InvalidProfileException>();
        }
        else
        {
            // The reference read never loads the bytes, so only content type, version and timestamp are checked there.
            (await getInfo()).Should().Be(new ProfilePictureInfo(contentType, version, Now));
        }
    }

    [Theory]
    [InlineData("image/gif", 1, 3)]
    [InlineData("image/png", 0, 3)]
    [InlineData("image/png", 1, 0)]
    public async Task ReplaceAsync_OverCorruptStoredRow_ThrowsCorruptAndLeavesRowUnchanged(string contentType, int version, int contentLength)
    {
        // Arrange
        await InsertRawAsync(contentType, version, new byte[contentLength]);

        // Act
        await using (var context = NewContext())
        {
            var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/png", NewBytes, Now.AddMinutes(1), CancellationToken.None);
            await act.Should().ThrowAsync<ProfilePictureCorruptException>();
            context.ChangeTracker.Entries<ProfilePicture>().Should().BeEmpty();
        }

        // Assert
        await using var fresh = NewContext();
        var raw = await fresh.ProfilePictures.AsNoTracking().Select(p => new { p.ContentType, p.Version }).SingleAsync();
        raw.ContentType.Should().Be(contentType);
        raw.Version.Should().Be(version);
    }

    [Fact]
    public async Task ReplaceAsync_ConcurrentReplacements_FirstCommitterWinsOtherGetsConflict()
    {
        // Arrange
        await SeedAsync();
        var barrier = new CompetingSaveBarrier();
        await using var first = NewContext(barrier);
        await using var second = NewContext(barrier);
        byte[] firstBytes = [0x10, 0x11];
        byte[] secondBytes = [0x20, 0x21];

        // Act
        var results = await Task.WhenAll(
            Capture(() => new ProfilePictureStore(first).ReplaceAsync(_profileId, "image/png", firstBytes, Now.AddMinutes(1), CancellationToken.None)),
            Capture(() => new ProfilePictureStore(second).ReplaceAsync(_profileId, "image/webp", secondBytes, Now.AddMinutes(1), CancellationToken.None)));

        // Assert
        results.Should().ContainSingle(r => r.Info != null).Which.Info!.Version.Should().Be(2);
        results.Should().ContainSingle(r => r.Error != null).Which.Error.Should().BeOfType<ProfilePictureConflictException>();
        var winnerBytes = results[0].Info != null ? firstBytes : secondBytes;
        var stored = await ReadAsync();
        stored!.Content.Should().Equal(winnerBytes);
        stored.Version.Should().Be(2);
    }

    [Fact]
    public async Task ReplaceAsync_ConcurrentFirstUploads_OneInsertsOtherGetsConflict()
    {
        // Arrange
        var barrier = new CompetingSaveBarrier();
        await using var first = NewContext(barrier);
        await using var second = NewContext(barrier);

        // Act
        var results = await Task.WhenAll(
            Capture(() => new ProfilePictureStore(first).ReplaceAsync(_profileId, "image/png", OldBytes, Now, CancellationToken.None)),
            Capture(() => new ProfilePictureStore(second).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now, CancellationToken.None)));

        // Assert
        results.Should().ContainSingle(r => r.Info != null).Which.Info!.Version.Should().Be(1);
        var conflict = results.Should().ContainSingle(r => r.Error != null).Which.Error.Should().BeOfType<ProfilePictureConflictException>().Subject;
        conflict.InnerException.Should().BeAssignableTo<DbUpdateException>();
        (await ReadAsync())!.Version.Should().Be(1);
    }

    [Fact]
    public async Task ReplaceAsync_RetryAfterConflict_SucceedsWithNextVersion()
    {
        // Arrange
        await SeedAsync();
        var barrier = new CompetingSaveBarrier();
        await using var first = NewContext(barrier);
        await using var second = NewContext(barrier);
        var stores = new[] { new ProfilePictureStore(first), new ProfilePictureStore(second) };
        var results = await Task.WhenAll(
            Capture(() => stores[0].ReplaceAsync(_profileId, "image/png", [0x10], Now.AddMinutes(1), CancellationToken.None)),
            Capture(() => stores[1].ReplaceAsync(_profileId, "image/png", [0x20], Now.AddMinutes(1), CancellationToken.None)));
        var loser = results[0].Error != null ? stores[0] : stores[1];

        // Act
        var retried = await loser.ReplaceAsync(_profileId, "image/png", [0x30], Now.AddMinutes(2), CancellationToken.None);

        // Assert
        results.Should().ContainSingle(r => r.Error is ProfilePictureConflictException);
        retried.Version.Should().Be(3);
        (await ReadAsync())!.Content.Should().Equal(0x30);
    }

    [Fact]
    public async Task ReplaceAsync_ForUnknownProfileId_ThrowsProfileNotFound()
    {
        // Arrange
        var unknown = Guid.NewGuid();

        // Act
        await using (var context = NewContext())
        {
            var act = () => new ProfilePictureStore(context).ReplaceAsync(unknown, "image/png", OldBytes, Now, CancellationToken.None);
            (await act.Should().ThrowAsync<PlayerProfileNotFoundException>()).Which.InnerException.Should().BeAssignableTo<DbUpdateException>();
        }

        // Assert
        await using var fresh = NewContext();
        (await fresh.ProfilePictures.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPictureAndReturnsTrue()
    {
        // Arrange
        await SeedAsync();

        // Act
        bool deleted;
        await using (var context = NewContext())
        {
            deleted = await new ProfilePictureStore(context).DeleteAsync(_profileId, CancellationToken.None);
        }

        // Assert
        deleted.Should().BeTrue();
        (await ReadAsync()).Should().BeNull();
        await using var fresh = NewContext();
        (await fresh.UserProfiles.AsNoTracking().AnyAsync(p => p.Id == _profileId)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenNone_ReturnsFalse()
    {
        // Act
        await using var context = NewContext();
        var deleted = await new ProfilePictureStore(context).DeleteAsync(_profileId, CancellationToken.None);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeletingProfile_CascadesToPicture()
    {
        // Arrange
        await SeedAsync();

        // Act
        await using (var context = NewContext())
        {
            await context.UserProfiles.Where(p => p.Id == _profileId).ExecuteDeleteAsync();
        }

        // Assert
        await using var fresh = NewContext();
        (await fresh.ProfilePictures.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReplaceAsync_WithPreCancelledToken_ThrowsAndKeepsPreviousPicture()
    {
        // Arrange
        await SeedAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        await using (var context = NewContext())
        {
            var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), cancellation.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        // Assert
        var stored = await ReadAsync();
        stored!.Content.Should().Equal(OldBytes);
        stored.Version.Should().Be(1);
    }

    [Fact]
    public async Task ReplaceAsync_CancelledDuringSave_ThrowsAndKeepsPreviousPicture()
    {
        // Arrange
        await SeedAsync();
        using var cancellation = new CancellationTokenSource();
        await using var context = NewContext(new CancelOnSave(cancellation));
        var store = new ProfilePictureStore(context);

        // Act
        var act = () => store.ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        context.ChangeTracker.Entries().Should().BeEmpty();
        var stored = await ReadAsync();
        stored!.Content.Should().Equal(OldBytes);
        stored.Version.Should().Be(1);
    }

    [Fact]
    public async Task ReplaceAsync_WithInvalidContentType_ThrowsBeforeWriting()
    {
        // Arrange
        await SeedAsync();
        var commands = new CommandRecorder();

        // Act
        await using (var context = NewContext(commands))
        {
            var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/svg+xml", NewBytes, Now.AddMinutes(1), CancellationToken.None);
            await act.Should().ThrowAsync<InvalidProfileException>();
        }

        // Assert
        commands.Texts.Should().NotContain(text => text.Contains("UPDATE", StringComparison.Ordinal) || text.Contains("INSERT", StringComparison.Ordinal));
        (await ReadAsync())!.Version.Should().Be(1);
    }

    private IdentityDbContext NewContext(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(_connectionString);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new IdentityDbContext(builder.Options);
    }

    private async Task SeedAsync()
    {
        await using var context = NewContext();
        await new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/png", OldBytes, Now, CancellationToken.None);
    }

    private async Task DeleteProfileAsync()
    {
        await using var other = NewContext();
        await other.UserProfiles.Where(p => p.Id == _profileId).ExecuteDeleteAsync();
    }

    private async Task InsertRawAsync(string contentType, int version, byte[] content)
    {
        await using var context = NewContext();
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO [ProfilePictures] ([ProfileId], [ContentType], [Content], [Version], [UpdatedAt]) VALUES ({_profileId}, {contentType}, {content}, {version}, {Now})");
    }

    private async Task<ProfilePicture?> ReadAsync()
    {
        await using var context = NewContext();
        return await new ProfilePictureStore(context).GetAsync(_profileId, CancellationToken.None);
    }

    private static async Task<(ProfilePictureInfo? Info, Exception? Error)> Capture(Func<Task<ProfilePictureInfo>> action)
    {
        try
        {
            return (await action(), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private static bool IsPictureWrite(DbCommand command) =>
        command.CommandText.Contains("INSERT INTO [ProfilePictures]", StringComparison.Ordinal)
        || command.CommandText.Contains("UPDATE [ProfilePictures]", StringComparison.Ordinal);

    /// <summary>Simulates the picture object write failing (INSERT/UPDATE on ProfilePictures never reaches SQL).</summary>
    private sealed class FailingPictureWrite : DbCommandInterceptor
    {
        public const string Message = "Simulated picture write failure.";

        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            Fail(command, result);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Fail(command, result);

        private ValueTask<T> Fail<T>(DbCommand command, T result)
        {
            if (IsPictureWrite(command))
            {
                Attempts++;
                throw new InvalidOperationException(Message);
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Fails the first commit after the picture row was written inside the transaction.</summary>
    private sealed class FailingCommit : DbTransactionInterceptor
    {
        public const string Message = "Simulated commit failure.";

        public int Failures { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Failures == 0)
            {
                Failures++;
                throw new InvalidOperationException(Message);
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Simulates a lost commit acknowledgement: the real commit has already succeeded when the commit step throws.
    /// An optional action runs in between, standing in for work another request commits before the outcome is checked.
    /// </summary>
    private sealed class LostCommitAcknowledgement(Func<Task>? afterCommit = null) : DbTransactionInterceptor
    {
        public const string Message = "Simulated lost commit acknowledgement.";

        public int Failures { get; private set; }

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Failures == 0)
            {
                Failures++;
                if (afterCommit is not null)
                {
                    await afterCommit();
                }

                throw new InvalidOperationException(Message);
            }
        }
    }

    /// <summary>Runs an action (on another connection) just before this context's SaveChanges.</summary>
    private sealed class RunBeforeSave(Func<Task> action) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await action();
            return result;
        }
    }

    /// <summary>Holds both contexts at SaveChanges until both have read the same row state.</summary>
    private sealed class CompetingSaveBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource<bool> _bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _bothRead.TrySetResult(true);
            }

            await _bothRead.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    /// <summary>Cancels the caller's token while the picture is being saved.</summary>
    private sealed class CancelOnSave(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await cancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> Texts { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Texts.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Texts.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
