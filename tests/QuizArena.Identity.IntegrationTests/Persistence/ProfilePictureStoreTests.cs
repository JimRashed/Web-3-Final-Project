using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuizArena.Identity.Data.Configurations;
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
        var sql = commands.Texts.Should().ContainSingle().Subject;
        sql.Should().Contain("DATALENGTH([p].[Content])", "the byte length is checked in SQL");
        sql.Replace("DATALENGTH([p].[Content])", string.Empty, StringComparison.Ordinal)
            .Should().NotContain("[Content]", "the reference read never selects the image bytes");
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
    public async Task ReplaceAsync_FirstUploadWhenAcknowledgementIsLostAndProfileWasDeleted_ThrowsProfileNotFound()
    {
        // Arrange: the first upload commits, then the profile (and by cascade the new row) is deleted before verification.
        var lostAck = new LostCommitAcknowledgement(DeleteProfileAsync);

        // Act
        await using var context = NewContext(lostAck);
        var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/png", OldBytes, Now, CancellationToken.None);

        // Assert: no row is not a confirmed rollback when the profile itself is gone.
        (await act.Should().ThrowAsync<PlayerProfileNotFoundException>())
            .Which.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(LostCommitAcknowledgement.Message);
        (await ReadAsync()).Should().BeNull();
    }

    [Fact]
    public async Task ReplaceAsync_WhenAcknowledgementIsLostAndVerificationReadFails_ThrowsCommitOutcomeUnknown()
    {
        // Arrange: the write commits, the acknowledgement is lost and the verification read fails too.
        await SeedAsync();
        var verificationFailure = new FailingVerificationRead();
        await using var context = NewContext(new LostCommitAcknowledgement(), verificationFailure);

        // Act
        var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);

        // Assert: inconclusive is not reported as the retry-safe original failure.
        (await act.Should().ThrowAsync<ProfilePictureCommitOutcomeUnknownException>())
            .Which.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(LostCommitAcknowledgement.Message);
        verificationFailure.Attempts.Should().Be(1);
        (await ReadAsync())!.Version.Should().Be(2, "the write did commit, which is why a retry could double-count");
    }

    [Fact]
    public async Task ReplaceAsync_UnderReadCommittedSnapshot_LostAcknowledgementIsVerifiedWithLockingRead()
    {
        // Arrange
        await EnableReadCommittedSnapshotAsync();
        var commands = new CommandRecorder();

        // Act: a first upload, then a replacement, each losing its commit acknowledgement.
        ProfilePictureInfo first;
        ProfilePictureInfo second;
        await using (var context = NewContext(commands, new LostCommitAcknowledgement()))
        {
            first = await new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/png", OldBytes, Now, CancellationToken.None);
        }

        await using (var context = NewContext(commands, new LostCommitAcknowledgement()))
        {
            second = await new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);
        }

        // Assert
        first.Should().Be(new ProfilePictureInfo("image/png", 1, Now));
        second.Should().Be(new ProfilePictureInfo("image/jpeg", 2, Now.AddMinutes(1)));
        commands.Texts.Count(IsVerificationRead).Should().Be(2);
        (await ReadAsync())!.Version.Should().Be(2);
    }

    [Fact]
    public async Task ReplaceAsync_UnderReadCommittedSnapshot_CommitFailureIsConfirmedRollbackAndRethrown()
    {
        // Arrange
        await EnableReadCommittedSnapshotAsync();
        await SeedAsync();
        await using var context = NewContext(new FailingCommit());

        // Act
        var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be(FailingCommit.Message);
        (await ReadAsync())!.Version.Should().Be(1);
    }

    [Fact]
    public async Task ReplaceAsync_UnderReadCommittedSnapshot_VerificationWaitsForInFlightWriteInsteadOfReadingPriorRow()
    {
        // Arrange: this call's commit fails (rolled back). Just before verification, another transaction changes the row
        // and commits a moment later. A snapshot read at that point still returns the prior row.
        await EnableReadCommittedSnapshotAsync();
        await SeedAsync();
        var inFlight = new InFlightWriteBeforeVerification(_connectionString, _profileId);
        await using var context = NewContext(new FailingCommit(), inFlight);

        // Act
        var act = () => new ProfilePictureStore(context).ReplaceAsync(_profileId, "image/jpeg", NewBytes, Now.AddMinutes(1), CancellationToken.None);
        var thrown = await act.Should().ThrowAsync<ProfilePictureConflictException>();
        await inFlight.Completion;

        // Assert: a snapshot read saw version 1 (a confirmed rollback by mistake); the locking read waited and saw the commit.
        thrown.Which.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(FailingCommit.Message);
        inFlight.SnapshotVersion.Should().Be(1);
        var stored = await ReadAsync();
        stored!.Version.Should().Be(2);
        stored.Content.Should().Equal(InFlightWriteBeforeVerification.Bytes);
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
    [InlineData("image/gif", 1, 3)]
    [InlineData("image/svg+xml", 1, 3)]
    [InlineData("image/png", 0, 3)]
    [InlineData("image/png", -4, 3)]
    [InlineData("image/png", 1, 0)]
    [InlineData("image/png", 1, ProfilePicture.MaxContentLength + 1)]
    public async Task Reads_WithCorruptStoredRow_FailLoudly(string contentType, int version, int contentLength)
    {
        // Arrange: bypass the domain and the CHECK constraints and write an invalid row directly.
        await DisableCheckConstraintsAsync();
        await InsertRawAsync(contentType, version, new byte[contentLength]);
        await using var context = NewContext();
        var store = new ProfilePictureStore(context);

        // Act
        var get = () => store.GetAsync(_profileId, CancellationToken.None);
        var getInfo = () => store.GetInfoAsync(_profileId, CancellationToken.None);

        // Assert: both reads reject the row; the reference read checks the stored length without loading the bytes.
        (await get.Should().ThrowAsync<ProfilePictureCorruptException>()).Which.InnerException.Should().BeOfType<InvalidProfileException>();
        (await getInfo.Should().ThrowAsync<ProfilePictureCorruptException>()).Which.InnerException.Should().BeOfType<InvalidProfileException>();
    }

    [Theory]
    [InlineData("image/gif", 1, 3)]
    [InlineData("image/png", 0, 3)]
    [InlineData("image/png", 1, 0)]
    public async Task ReplaceAsync_OverCorruptStoredRow_ThrowsCorruptAndLeavesRowUnchanged(string contentType, int version, int contentLength)
    {
        // Arrange
        await DisableCheckConstraintsAsync();
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

    [Theory]
    [InlineData("image/png", 1, 0, ProfilePictureConfiguration.ContentLengthConstraint)]
    [InlineData("image/png", 1, ProfilePicture.MaxContentLength + 1, ProfilePictureConfiguration.ContentLengthConstraint)]
    [InlineData("image/gif", 1, 3, ProfilePictureConfiguration.ContentTypeConstraint)]
    [InlineData("image/svg+xml", 1, 3, ProfilePictureConfiguration.ContentTypeConstraint)]
    [InlineData("IMAGE/PNG", 1, 3, ProfilePictureConfiguration.ContentTypeConstraint)]
    [InlineData("image/png", 0, 3, ProfilePictureConfiguration.VersionConstraint)]
    [InlineData("image/png", -4, 3, ProfilePictureConfiguration.VersionConstraint)]
    public async Task Database_RejectsRawWriteOfInvalidPicture_WithCheckConstraintViolation(string contentType, int version, int contentLength, string constraint)
    {
        // Act: bypass the domain; the database itself must refuse the row.
        var insert = () => InsertRawAsync(contentType, version, new byte[contentLength]);

        // Assert
        var error = (await insert.Should().ThrowAsync<SqlException>()).Which;
        error.Number.Should().Be(547, "547 is SQL Server's constraint-violation error");
        error.Message.Should().Contain(constraint);
        await using var fresh = NewContext();
        (await fresh.ProfilePictures.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Database_RejectsRawUpdateThatWouldCorruptAValidPicture()
    {
        // Arrange
        await SeedAsync();
        await using var context = NewContext();

        // Act
        var update = () => context.Database.ExecuteSqlAsync($"UPDATE [ProfilePictures] SET [Version] = 0 WHERE [ProfileId] = {_profileId}");

        // Assert
        (await update.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
        (await ReadAsync())!.Version.Should().Be(1);
    }

    [Fact]
    public async Task Database_AcceptsRawWriteAtTheMaximumSize()
    {
        // Act
        await InsertRawAsync("image/webp", 1, new byte[ProfilePicture.MaxContentLength]);

        // Assert
        var stored = await ReadAsync();
        stored!.Content.Should().HaveCount(ProfilePicture.MaxContentLength);
        stored.ContentType.Should().Be("image/webp");
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

    /// <summary>
    /// Disables the picture CHECK constraints in this test's own throwaway database. The constraints make corrupt rows
    /// impossible to write, but read-side validation (Rehydrate/RehydrateInfo) remains a second line of defence for rows
    /// written while checks were off, so these tests need a way to plant such a row.
    /// </summary>
    private async Task DisableCheckConstraintsAsync()
    {
        await using var context = NewContext();
        await context.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE [ProfilePictures] NOCHECK CONSTRAINT [{ProfilePictureConfiguration.ContentLengthConstraint}], [{ProfilePictureConfiguration.ContentTypeConstraint}], [{ProfilePictureConfiguration.VersionConstraint}]");
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

    /// <summary>Turns on READ_COMMITTED_SNAPSHOT (the Azure SQL default) for this test's database.</summary>
    private async Task EnableReadCommittedSnapshotAsync()
    {
        // The ALTER needs the database to itself: drop idle pooled connections first and use an unpooled one.
        await using (var pooled = new SqlConnection(_connectionString))
        {
            SqlConnection.ClearPool(pooled);
        }

        await using var connection = new SqlConnection(new SqlConnectionStringBuilder(_connectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync();
        await using (var alter = connection.CreateCommand())
        {
            alter.CommandText = "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE";
            await alter.ExecuteNonQueryAsync();
        }

        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()";
        ((bool)(await check.ExecuteScalarAsync())!).Should().BeTrue();
    }

    private static bool IsVerificationRead(string commandText) =>
        commandText.Contains("[ProfilePictures] WITH (READCOMMITTEDLOCK)", StringComparison.Ordinal);

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

    /// <summary>Fails the commit-outcome verification read, leaving the outcome inconclusive.</summary>
    private sealed class FailingVerificationRead : DbCommandInterceptor
    {
        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (IsVerificationRead(command.CommandText))
            {
                Attempts++;
                throw new TimeoutException("Simulated verification read timeout.");
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Just before the verification read, another connection updates the picture inside an open transaction and commits
    /// it a moment later, standing in for a commit SQL Server is still completing. Records what a snapshot read sees then.
    /// </summary>
    private sealed class InFlightWriteBeforeVerification(string connectionString, Guid profileId) : DbCommandInterceptor
    {
        public static readonly byte[] Bytes = [0x77, 0x78];

        private Task _completion = Task.CompletedTask;

        public int? SnapshotVersion { get; private set; }

        public Task Completion => _completion;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!IsVerificationRead(command.CommandText) || SnapshotVersion is not null)
            {
                return result;
            }

            var writer = new SqlConnection(connectionString);
            await writer.OpenAsync(CancellationToken.None);
            var transaction = (SqlTransaction)await writer.BeginTransactionAsync(CancellationToken.None);
            await using (var update = writer.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE [ProfilePictures] SET [ContentType] = 'image/webp', [Content] = @content, [Version] = 2 WHERE [ProfileId] = @id";
                update.Parameters.AddWithValue("@content", Bytes);
                update.Parameters.AddWithValue("@id", profileId);
                await update.ExecuteNonQueryAsync(CancellationToken.None);
            }

            // Under READ_COMMITTED_SNAPSHOT a plain read does not wait for the open transaction: it sees the prior row.
            await using (var reader = new SqlConnection(connectionString))
            {
                await reader.OpenAsync(CancellationToken.None);
                await using var select = reader.CreateCommand();
                select.CommandText = "SELECT [Version] FROM [ProfilePictures] WHERE [ProfileId] = @id";
                select.CommandTimeout = 5;
                select.Parameters.AddWithValue("@id", profileId);
                SnapshotVersion = (int)(await select.ExecuteScalarAsync(CancellationToken.None))!;
            }

            _completion = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                await transaction.CommitAsync();
                await transaction.DisposeAsync();
                await writer.DisposeAsync();
            });
            return result;
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
