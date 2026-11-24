# Issue #143 plan: Identity-owned picture reference and private persistence

> Agent-reviewed plan from the Dispatcher pipeline (Plan stage). Not human-approved; final human review happens on the draft PR.

- Issue: [JimRashed/Web-3-Final-Project#143](https://github.com/JimRashed/Web-3-Final-Project/issues/143) `[SEC][C1]`, parent #135, blocks #144, blocked by #137 (merged, PR #149).
- Branch: `feat/143-identity-picture-persistence` from `feature/milestone-4-security-and-cross-device-profiles` @ `fb60334`.
- Binding decisions: `docs/milestone3/security-decisions.md` SEC-03 to SEC-07, the `ProfilePictures` table, and DR-3.
- Prime brief: `.dispatcher/runs/143-202611221226/context.md` (verified against the code at `fb60334`).

## 1. Summary and architecture

Add private, Identity-owned storage for one profile picture per player in the Identity SQL Server database, plus the
application methods that #144 will call from HTTP endpoints. No HTTP surface changes in this issue.

Behaviour:
- A picture belongs to exactly one `UserProfiles` row (`ProfilePictures.ProfileId` is PK and FK, cascade delete).
- The owner is resolved only from the validated token's exact issuer + subject (`IPlayerProfileRepository.FindByIdentityAsync`,
  BIN2 columns). Callers never pass a profile id, key, path or URL.
- Replace is atomic in one explicit transaction: the reader sees the old picture or the new one, never a partial one.
  A failed write or failed commit leaves the previous picture (or no picture) in place.
- Concurrency (SEC-07): first committer wins. The loser of the `rowversion` check, or of the first-upload primary-key
  race, gets `ProfilePictureConflictException` (409 in #144) and may retry. Sequential replacements each succeed and
  `Version` grows by exactly 1 (starts at 1).
- The stored reference exposed to callers is `ProfilePictureInfo(ContentType, Version, UpdatedAt)`: no key, path or
  URL (SEC-03). Bytes leave the store only through `GetMyPictureAsync` for the owner.
- DR-3: transient SQL failures (and `TimeoutException`) on Identity map to 503 problem+json, as Lobby does.

Layer boundaries (unchanged project references; ArchitectureTests `ServiceBoundaryTests.cs` forbid new Domain references):
- **Domain** (`QuizArena.Identity.Domain`): `ProfilePicture` entity, `ProfilePictureInfo` record, `IProfilePictureStore`
  port, two exceptions. No EF or SqlClient types.
- **Data** (`QuizArena.Identity.Data`): EF configuration, `ProfilePictureStore`, DI registration, migration `AddProfilePictures`.
- **Api** (`QuizArena.Identity.Api`): `IProfileApplication.GetMyPictureAsync` / `ReplaceMyPictureAsync`, and the 503
  mapping in `IdentityExceptionHandler`. No controller, route, DTO or contract change.

### Assumptions and interpretations (record in the PR body)
1. **"Extend `PlayerProfile`" (AC1)** is met by a separate profile-owned entity `ProfilePicture` keyed by
   `PlayerProfile.Id`, not by adding fields or a navigation to the `PlayerProfile` aggregate. Reason: the agreed table
   (SEC-05) is a separate `ProfilePictures` table; loading 2 MiB of bytes with every profile read, and making profile
   rename contend with picture `rowversion`, would be wrong. `PlayerProfile.cs` stays unchanged.
2. Content size (<= 2,097,152 bytes) and magic-byte validation are API-enforced in #144 (SEC-05/06). The domain entity
   still guards the hard invariants (non-empty, <= `MaxContentLength`, allowed content type) so a bad row fails loudly.
3. `ContentType` column: `nvarchar(32)`, required, no SQL CHECK constraint (the domain allow-list is the rule; a CHECK
   would make the allow-list a migration concern). Recorded as a small choice.
4. `RowVersion` is an EF shadow property (`byte[]`, `IsRowVersion()`), so the domain stays free of persistence tokens.
5. Profile missing: the application throws `PlayerProfileNotFoundException` when the caller has no profile (404 in #144);
   the store maps SQL error 547 (FK violation, profile deleted mid-request) to the same exception.
6. #137's approval record lists no review by SuSu. Noted for the PR body; not a blocker for this issue (Dispatcher decision).

### Out of scope here (all #144 or later)
HTTP endpoints `GET/PUT/DELETE /api/users/me/picture`, `ProfileResponse.picture`, magic-byte/size validation at the
boundary, 409/404 HTTP mapping of the new exceptions, `x-implemented-by` / contract marker changes in
`contracts/openapi/identity.yaml`, client work (#145).

## 2. File inventory

Create (production):
| File | Purpose |
|---|---|
| `src/QuizArena.Identity.Domain/Entities/ProfilePicture.cs` | Entity: `ProfileId`, `ContentType`, `Content`, `Version`, `UpdatedAt`; `Create`, `Replace`, `Rehydrate`, `ToInfo()` |
| `src/QuizArena.Identity.Domain/Entities/ProfilePictureInfo.cs` | `sealed record ProfilePictureInfo(string ContentType, int Version, DateTime UpdatedAt)` (the approved reference, SEC-03) |
| `src/QuizArena.Identity.Domain/Repositories/IProfilePictureStore.cs` | Port: get info / get / replace / delete by profile id |
| `src/QuizArena.Identity.Domain/Exceptions/ProfilePictureConflictException.cs` | Lost a concurrent replace (409 in #144); derives `IdentityDomainException` |
| `src/QuizArena.Identity.Domain/Exceptions/PlayerProfileNotFoundException.cs` | Caller/profile has no `UserProfiles` row (404 in #144) |
| `src/QuizArena.Identity.Data/Configurations/ProfilePictureConfiguration.cs` | Table mapping, FK cascade, shadow `RowVersion` |
| `src/QuizArena.Identity.Data/Repositories/ProfilePictureStore.cs` | EF implementation with explicit transaction and SQL error mapping |
| `src/QuizArena.Identity.Data/Migrations/<timestamp>_AddProfilePictures.cs` + `.Designer.cs` | Generated by dotnet-ef; additive only |

Modify (production and scripts):
| File | Change |
|---|---|
| `src/QuizArena.Identity.Data/Context/IdentityDbContext.cs` | Add `DbSet<ProfilePicture> ProfilePictures` |
| `src/QuizArena.Identity.Data/Migrations/IdentityDbContextModelSnapshot.cs` | Regenerated by dotnet-ef |
| `src/QuizArena.Identity.Data/IdentityPersistenceExtensions.cs` | `services.AddScoped<IProfilePictureStore, ProfilePictureStore>()` |
| `src/QuizArena.Identity.Api/Services/IProfileApplication.cs` | Add `GetMyPictureAsync`, `ReplaceMyPictureAsync` |
| `src/QuizArena.Identity.Api/Services/ProfileApplication.cs` | Inject `IProfilePictureStore`; implement both methods |
| `src/QuizArena.Identity.Api/Services/IdentityExceptionHandler.cs` | DR-3: 503 for transient SQL / timeout (Lobby pattern) |
| `scripts/migrate-legacy-data/Invoke-LegacyMigration.ps1` | Rollback never deletes a profile that has a `ProfilePictures` row |
| `scripts/migrate-legacy-data/Invoke-Rehearsal.ps1` | Rollback rehearsal: imported profile with a picture is kept |
| `docs/milestone3/data-cutover.md` | Document the rollback guard (Rollback section and rehearsal table) |

Create / modify (tests, existing projects only):
| File | Change |
|---|---|
| `tests/QuizArena.Identity.UnitTests/Domain/ProfilePictureTests.cs` | New: entity invariants and versioning |
| `tests/QuizArena.Identity.IntegrationTests/Persistence/ProfilePictureStoreTests.cs` | New: LocalDB persistence, failure injection, concurrency |
| `tests/QuizArena.Identity.IntegrationTests/Persistence/MigrationTests.cs` | Add preservation up/down test for `AddProfilePictures` |
| `tests/QuizArena.Identity.IntegrationTests/Persistence/ProfilePictureFreshInstanceTests.cs` | New: two hosts sharing one database |
| `tests/QuizArena.Identity.IntegrationTests/Application/ProfileApplicationTests.cs` | Add ownership tests; update constructor calls for the new dependency |
| `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileErrorTests.cs` | New: DR-3 503 on `GET /api/users/me` (and PUT) |

No deletes. `PlayerProfile.cs`, `ProfileResponse.cs`, controllers, `contracts/**` and `specs/implementation-plan.md` are not touched.

## 3. Ordered steps

### Step 1: Domain types
Files: `ProfilePicture.cs`, `ProfilePictureInfo.cs`, `IProfilePictureStore.cs`, the two exceptions.
- `ProfilePicture` (sealed, private ctor, mirrors `PlayerProfile` style):
  - `public const int MaxContentLength = 2_097_152;` `public static IReadOnlySet<string> AllowedContentTypes` =
    `image/png`, `image/jpeg`, `image/webp` (ordinal, exact; SVG/GIF absent).
  - `Create(Guid profileId, string contentType, byte[] content, DateTime updatedAt)` → `Version = 1`.
  - `Replace(string contentType, byte[] content, DateTime updatedAt)` → validates first, then sets all fields and
    `Version + 1` (validation failure leaves the instance unchanged).
  - `Rehydrate(...)` re-checks invariants (`Version >= 1`, UTC, allowed type, length 1..Max) so corrupt rows fail loudly.
  - Content is copied on `Create`/`Replace` (caller buffers cannot mutate stored state). EF maps `Content` via a
    private setter.
  - Invariant failures throw `InvalidProfileException(field, message)` (reuse; maps to 400 already).
  - `ToInfo()` → `ProfilePictureInfo`.
- `IProfilePictureStore` (all async with `CancellationToken`):
  - `Task<ProfilePictureInfo?> GetInfoAsync(Guid profileId, CancellationToken)` (projection, no bytes).
  - `Task<ProfilePicture?> GetAsync(Guid profileId, CancellationToken)`.
  - `Task<ProfilePictureInfo> ReplaceAsync(Guid profileId, string contentType, byte[] content, DateTime updatedAt, CancellationToken)`.
  - `Task<bool> DeleteAsync(Guid profileId, CancellationToken)`.
- `ProfilePictureConflictException()` and `PlayerProfileNotFoundException()` derive `IdentityDomainException`, with fixed
  safe messages (no ids or subjects in messages).
Check: `dotnet build QuizArena.sln --no-restore -warnaserror`; ArchitectureTests still pass (no new references).

### Step 2: Data mapping, store and DI
Files: `ProfilePictureConfiguration.cs`, `IdentityDbContext.cs`, `ProfilePictureStore.cs`, `IdentityPersistenceExtensions.cs`.
- Configuration: `ToTable("ProfilePictures")`; `HasKey(ProfileId)`, `ValueGeneratedNever()`;
  `HasOne<PlayerProfile>().WithOne().HasForeignKey<ProfilePicture>(p => p.ProfileId).OnDelete(DeleteBehavior.Cascade)`
  (no navigation on either side); `ContentType` `HasMaxLength(32)` required; `Content` required `varbinary(max)`
  (`HasColumnType("varbinary(max)")`); `Version` required int; `UpdatedAt` with `UtcConverter.Instance`;
  `Property<byte[]>("RowVersion").IsRowVersion()`. Columns match the SEC-05 table exactly.
- `IdentityDbContext`: `public DbSet<ProfilePicture> ProfilePictures => Set<ProfilePicture>();`.
- `ProfilePictureStore(IdentityDbContext context) : IProfilePictureStore`:
  - `GetInfoAsync`: `AsNoTracking` projection to `ProfilePictureInfo` (does not read `Content`).
  - `GetAsync`: `AsNoTracking().SingleOrDefaultAsync(p => p.ProfileId == profileId)`.
  - `ReplaceAsync`: `await using var tx = await context.Database.BeginTransactionAsync(ct)`; load tracked row; if null
    `ProfilePicture.Create(...)` + `Add`, else `existing.Replace(...)` (domain validation runs before any SQL write);
    `SaveChangesAsync(ct)`; `tx.CommitAsync(ct)`; return `ToInfo()`. The `UPDATE` carries `WHERE RowVersion = @original`.
  - Error mapping (constants as in `PlayerProfileRepository`): `DbUpdateConcurrencyException` → `ProfilePictureConflictException`;
    `DbUpdateException` with inner `SqlException` 2601/2627 → `ProfilePictureConflictException` (first-upload PK race);
    547 → `PlayerProfileNotFoundException`. Inner exception preserved. Any other exception (including commit failure and
    `OperationCanceledException`) propagates unchanged; the undisposed transaction rolls back on dispose.
  - On every failure path call `context.ChangeTracker.Clear()` (in `catch`/`finally`) so a retry in the same scope
    starts clean (SEC-07 "may retry").
  - `DeleteAsync`: `ExecuteDeleteAsync` by `ProfileId`; returns `rows > 0`.
- DI: `services.AddScoped<IProfilePictureStore, ProfilePictureStore>();` next to the repository line.
Check: build passes; `MigrationTests.Model_ComparedWithLatestMigration_HasNoUnmigratedChanges` fails until Step 3 (expected).

### Step 3: Migration `AddProfilePictures`
Command (from the worktree root, tool manifest pins dotnet-ef 10.0.12):
```
dotnet tool restore
dotnet tool run dotnet-ef migrations add AddProfilePictures --project src/QuizArena.Identity.Data --startup-project src/QuizArena.Identity.Api --context IdentityDbContext --output-dir Migrations
```
- Review generated `Up`: only `CreateTable("ProfilePictures")` with PK `PK_ProfilePictures`, FK
  `FK_ProfilePictures_UserProfiles_ProfileId` (cascade), `RowVersion rowversion`. No `AlterColumn`/`DropIndex` on
  `UserProfiles`. `Down`: only `DropTable("ProfilePictures")`.
- Keep generated files as produced (then `dotnet format`); do not hand-edit the snapshot.
Check: `HasPendingModelChanges` false; fresh-database migration test passes.

### Step 4: Application methods (owner by exact iss/sub)
Files: `IProfileApplication.cs`, `ProfileApplication.cs`.
- Constructor becomes `ProfileApplication(IPlayerProfileRepository profiles, IProfilePictureStore pictures, TimeProvider clock)`
  (DI already registers all three; `Program.cs` unchanged).
- `Task<ProfilePicture?> GetMyPictureAsync(AuthenticatedActor actor, CancellationToken ct)`: `EnsurePlayer`; profile =
  `FindByIdentityAsync(actor.Issuer, actor.Subject)`; null profile → return null (#144 maps "no profile or no picture" to
  404, per contract line 408); else `pictures.GetAsync(profile.Id)`.
- `Task<ProfilePictureInfo> ReplaceMyPictureAsync(AuthenticatedActor actor, string contentType, byte[] content, CancellationToken ct)`:
  `EnsurePlayer`; resolve profile the same way; null → `PlayerProfileNotFoundException` (no implicit profile creation);
  `now = clock.GetUtcNow().UtcDateTime`; `pictures.ReplaceAsync(profile.Id, contentType, content, now)`.
- XML docs state: the profile id always comes from the validated issuer + subject; no key/path/URL is returned.
Check: build; existing `ProfileApplicationTests` updated to pass the new strict mock.

### Step 5: DR-3, transient SQL → 503
File: `IdentityExceptionHandler.cs` (add `using Microsoft.Data.SqlClient;` — Api already gets SqlClient transitively via Data).
- Keep existing cancellation and `InvalidProfileException` → 400 behaviour exactly.
- Add: when `IsDependencyUnavailable(exception)` → 503, write `ProblemDetails { Status = 503, Title = "Dependency unavailable" }`
  (title matches identity.yaml's 503 description) through `problems.TryWriteAsync`. Copy Lobby's
  `IsDependencyUnavailable` (walks `InnerException`; `TimeoutException`; `SqlException` numbers
  -2, 2, 53, 64, 233, 4060, 10053, 10054, 10060, 11001, 40197, 40501, 40613). Skip when `Response.HasStarted`.
- Everything else still falls through to the default 500. New picture exceptions are not mapped here (#144).
Check: `ProfileErrorTests` (Step 7) green; no response body contains exception text.

### Step 6: Legacy rollback guard
Files: `Invoke-LegacyMigration.ps1`, `Invoke-Rehearsal.ps1`, `docs/milestone3/data-cutover.md`.
- In the Rollback branch, before the delete batch: fail closed if `OBJECT_ID('{IDENTITY}.dbo.ProfilePictures')` is null
  (`throw 'Identity database is not migrated to the latest schema (ProfilePictures missing); run dotnet-ef database update first.'`).
  A missing table would otherwise make the statement fail at runtime anyway; an explicit message is clearer.
- Profiles `DELETE ... WHERE` gains
  `AND NOT EXISTS (SELECT 1 FROM {IDENTITY}.dbo.ProfilePictures pp WHERE pp.ProfileId = p.Id)`, so cascade delete can
  never remove an uploaded picture. Kept rows stay `Imported` and appear in the existing `KeptBecauseChanged` count.
- Rehearsal: before the rollback step insert one `ProfilePictures` row for alice's imported profile
  (`0x89504E47...`, `image/png`, `Version 1`, `SYSUTCDATETIME()`); expectations become "alice kept (has a picture),
  bob kept" = 2 profiles and 1 picture. Add a second assertion that the picture row still exists. Rehearsal already
  migrates every service database to the latest migration (line 82), so the table exists.
- `data-cutover.md`: Rollback bullet adds "or a profile that has an uploaded picture"; rehearsal table row adds the case.
Check: PowerShell parses (`[System.Management.Automation.Language.Parser]::ParseFile` with no errors); rehearsal run when
LocalDB is available (manual evidence, see §6).

### Step 7: Tests (details in §5)
Files: the test files listed in §2. Order: unit tests (Step 1) → store tests (Steps 2-3) → migration test (Step 3) →
application ownership tests (Step 4) → fresh-instance tests → 503 tests (Step 5).
Check: all Identity unit, integration and architecture tests pass locally on LocalDB.

### Step 8: Validate and record evidence
Run §6 commands in order; capture pass/fail counts from this run only (never reuse historical counts). PR body lists the
assumptions in §1, the SuSu note, and that HTTP endpoints / `ProfileResponse.picture` are #144.

## 4. Acceptance coverage

| # | Acceptance criterion | Steps | Evidence (tests) |
|---|---|---|---|
| AC1 | Extend `PlayerProfile`, `IdentityDbContext`, and the repository/application boundary as agreed | 1, 2, 3, 4 | `ProfilePictureTests`; `ProfilePictureStoreTests`; `MigrationTests` model check; `ProfileApplicationTests` picture cases. Interpretation of "extend PlayerProfile" recorded (§1 assumption 1) |
| AC2 | Preserve existing profiles through migration; use the exact trusted issuer/subject account key | 3, 4, 6 | `MigrationTests.AddProfilePictures_PreservesExistingProfilesUpAndDown`; `ProfileApplicationTests.*CaseVariantSubject*` / `*UsesProfileIdFromExactIdentity*`; `ProfilePictureFreshInstanceTests.ReplaceMyPicture_ForCaseVariantSubjects_KeepsPicturesSeparate`; rollback guard + rehearsal |
| AC3 | Current image reference and bytes survive a fresh service instance using shared storage | 2, 3, 4, 7 | `ProfilePictureFreshInstanceTests.ReplaceMyPicture_InFirstHost_IsReadableFromSecondHost` (bytes, content type, version, updatedAt) |
| AC4 | First upload and replacement persistence, incl. object-write failure, profile-reference commit failure, agreed concurrent behaviour | 2, 7 | `ProfilePictureStoreTests`: first upload, replace, sequential, write-failure, commit-failure, concurrent replace, first-upload race |
| AC5 | Saved profile always references a readable image or its prior state; cleanup or retry handling | 2, 6, 7 | Failure tests assert prior bytes/version readable from a fresh context; `*RetryAfterConflict_Succeeds*`; `ChangeTracker.Clear` reuse; cancellation leaves prior state; delete/cascade; rollback guard |
| AC6 | Images private; expose only the approved reference or access mechanism | 1, 4 | `ProfilePictureInfo` has only ContentType/Version/UpdatedAt (unit reflection test); application exposes bytes only to the exact owner; no endpoint/URL added (endpoints in #144) |

DR-3 (routed to this issue by the decision doc): Step 5, `ProfileErrorTests`.

## 5. Testing strategy

xUnit + FluentAssertions + Moq, AAA comments, descriptive `Method_Condition_Result` names, as in the existing Identity tests.
Persistence tests use a per-class LocalDB database `QuizArenaIdentity_Picture_<guid>` (honours `QUIZARENA_TEST_SQLSERVER`),
`MigrateAsync` in `InitializeAsync`, `EnsureDeletedAsync` in `DisposeAsync`, and fresh contexts for every read-back.

**Unit: `tests/QuizArena.Identity.UnitTests/Domain/ProfilePictureTests.cs`**
- `Create_WithValidInput_StartsAtVersionOne`; `Replace_IncrementsVersionByOneAndReplacesFields`.
- `Create_WithEachAllowedContentType_Succeeds` (theory png/jpeg/webp); `Create_WithDisallowedContentType_Throws`
  (theory `image/svg+xml`, `image/gif`, `IMAGE/PNG`, `""`, `" image/png"`).
- `Create_WithContentAtExactMax_Succeeds`; `Create_WithContentOverMax_Throws`; `Create_WithEmptyContent_Throws`.
- `Create_WithEmptyProfileId_Throws`; `Create_WithNonUtcTimestamp_Throws` (Local/Unspecified/default).
- `Replace_WithInvalidInput_LeavesPictureUnchanged`; `Create_CopiesContent_CallerMutationDoesNotLeak`.
- `Rehydrate_WithCorruptValues_Throws` (version 0, non-UTC, bad type); `Rehydrate_WithValidRow_RoundTrips`.
- `ProfilePictureInfo_ExposesOnlyContentTypeVersionAndUpdatedAt` (reflection over public properties: no key/path/URL/bytes).

**Persistence: `tests/QuizArena.Identity.IntegrationTests/Persistence/ProfilePictureStoreTests.cs`**
Seed a profile via `PlayerProfileRepository.AddIfAbsentAsync`. Contexts built with optional interceptors.
- `ReplaceAsync_FirstUpload_PersistsVersionOneReadableFromFreshContext`.
- `ReplaceAsync_WhenPictureExists_ReplacesBytesAndIncrementsVersion`; `ReplaceAsync_SequentialReplacements_EachSucceedsVersionIncrementsByOne` (1→2→3).
- `GetInfoAsync_ReturnsReferenceWithoutBytes`; `GetAsync_WhenNone_ReturnsNull`.
- Object-write failure: `ReplaceAsync_WhenObjectWriteFails_KeepsPreviousPicture` — `DbCommandInterceptor` throwing in
  `ReaderExecutingAsync`/`NonQueryExecutingAsync` for commands touching `ProfilePictures` with `INSERT`/`UPDATE`;
  assert the exception propagates and a fresh context reads the prior bytes and version (also a first-upload variant:
  no row afterwards).
- Commit failure: `ReplaceAsync_WhenCommitFails_RollsBackToPreviousPicture` — `DbTransactionInterceptor` throwing in
  `TransactionCommittingAsync` after the `UPDATE` ran; assert prior state, and that the same store instance can retry
  successfully afterwards (tracker cleared).
- Concurrent replace: `ReplaceAsync_ConcurrentReplacements_FirstCommitterWinsOtherGetsConflict` — two contexts with a
  shared `CompetingSaveBarrier` (`SaveChangesInterceptor`, copy of Lobby's) so both read the same `RowVersion`; exactly one
  result succeeds with `Version == 2`, the other throws `ProfilePictureConflictException`; stored bytes equal the winner's.
- First-upload race: `ReplaceAsync_ConcurrentFirstUploads_OneInsertsOtherGetsConflict` — same barrier, no existing row;
  one row with `Version == 1`, loser gets `ProfilePictureConflictException` (2627).
- Retry: `ReplaceAsync_RetryAfterConflict_SucceedsWithNextVersion` (loser retries in the same scope → `Version == 3`).
- Profile missing: `ReplaceAsync_ForUnknownProfileId_ThrowsProfileNotFound` (547 mapping), no row created.
- Delete/cascade: `DeleteAsync_RemovesPictureAndReturnsTrue`, `DeleteAsync_WhenNone_ReturnsFalse`,
  `DeletingProfile_CascadesToPicture` (delete `UserProfiles` row via fresh context, picture gone).
- Cancellation: `ReplaceAsync_WithCancelledToken_ThrowsAndKeepsPreviousPicture` (pre-cancelled token, and a token cancelled
  inside a `SavingChangesAsync` interceptor).

**Migration: add to `tests/QuizArena.Identity.IntegrationTests/Persistence/MigrationTests.cs`**
- `AddProfilePictures_PreservesExistingProfilesUpAndDown`: `GetService<IMigrator>().MigrateAsync("AddUserProfiles")`;
  seed `UserProfiles` with raw SQL (two subjects differing only in case, fixed ids/timestamps); migrate to latest → all
  profile rows byte-for-byte unchanged and `ProfilePictures` exists and is empty; insert one picture; migrate back to
  `AddUserProfiles` → `ProfilePictures` gone (`OBJECT_ID` null), profiles unchanged, `UX_UserProfiles_Issuer_Subject` intact.
- Existing two tests keep passing (fresh-database lineage, no pending model changes).

**Application ownership: `tests/QuizArena.Identity.IntegrationTests/Application/ProfileApplicationTests.cs`**
Add `Mock<IProfilePictureStore>(MockBehavior.Strict)` to the fixture and constructor.
- `ReplaceMyPictureAsync_UsesProfileIdFromExactIdentity` — store `ReplaceAsync` verified once with the resolved
  profile's `Id` and the clock's UTC time.
- `ReplaceMyPictureAsync_ForCaseVariantSubject_DoesNotReachOtherProfile` — repo returns the profile only for
  `player-a`; actor `Player-A` → `PlayerProfileNotFoundException`, store never called.
- `ReplaceMyPictureAsync_WithoutProfile_ThrowsProfileNotFound`; `GetMyPictureAsync_WithoutProfile_ReturnsNull`;
  `GetMyPictureAsync_ReturnsOwnersPicture`.
- `PictureOperations_ForMachineActor_Throw` (theory over both methods), store and repo untouched.
- `ReplaceMyPictureAsync_WhenStoreReportsConflict_PropagatesConflict` (no swallowing; #144 maps to 409).

**Fresh instance: `tests/QuizArena.Identity.IntegrationTests/Persistence/ProfilePictureFreshInstanceTests.cs`**
Uses `IdentityApiFactory` (class fixture; one migrated database). Hosts are derived with
`factory.WithWebHostBuilder(_ => { })`, so each has its own DI container and DbContext pool but the same connection string.
- `ReplaceMyPicture_InFirstHost_IsReadableFromSecondHost`: host A scope → `IProfileApplication.UpsertMeAsync` then
  `ReplaceMyPictureAsync`; dispose host A; host B scope → `GetMyPictureAsync` returns identical bytes, content type,
  version and `UpdatedAt`; `GetInfoAsync` matches.
- `ReplaceMyPicture_ForCaseVariantSubjects_KeepsPicturesSeparate`: two players `subject-x` / `Subject-X`; each uploads;
  each reads back only their own bytes (real BIN2 lookup end to end).

**DR-3: `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileErrorTests.cs`**
Pattern from `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyErrorTests.cs` (copy its reflection `SqlFailure(number)`
helper; no network I/O). Replace `IProfileApplication` via `ConfigureTestServices` with a stub that throws.
- `GetMe_WhenDependencyUnavailable_Returns503Problem` (theory: `SqlFailure(-2)`, `SqlFailure(53)`, `TimeoutException`,
  `DbUpdateException` wrapping `SqlFailure(53)`): status 503, `application/problem+json`, body has no sentinel/exception text.
- `PutMe_WhenDependencyUnavailable_Returns503Problem` (one case).
- `GetMe_WhenNonTransientSqlError_Returns500` (`SqlFailure(2601)`) and `GetMe_WhenUnexpectedError_Returns500` — unchanged fallback.
- Existing `ProfileEndpointsTests` 400 cases continue to pass (regression).
Token/auth: reuse `IdentityApiFactory` + `TestTokens` as `ProfileEndpointsTests` does.

**Legacy script**: no xUnit tests exist for it; `Invoke-Rehearsal.ps1` is its test harness and is extended (Step 6).

## 6. Validation commands

Environment: .NET 10 SDK, LocalDB `(localdb)\MSSQLLocalDB` (or `QUIZARENA_TEST_SQLSERVER`), Node for contracts. Run from
`C:/Web-3-Final-Project-wt/143`:
```
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test tests/QuizArena.Identity.UnitTests --no-build
dotnet test tests/QuizArena.Identity.IntegrationTests --no-build
dotnet test tests/QuizArena.ArchitectureTests --no-build
node contracts/validate-contracts.mjs
```
Migration creation (Step 3 only): `dotnet tool restore` then the dotnet-ef command in Step 3.
Optional manual evidence (needs LocalDB and permission to create databases):
`pwsh scripts/migrate-legacy-data/Invoke-Rehearsal.ps1` (or `powershell -File ...`) → `REHEARSAL PASSED`.
Static check of both scripts: `[System.Management.Automation.Language.Parser]::ParseFile(<path>, [ref]$null, [ref]$errors)` with zero errors.
Contracts are untouched, so `validate-contracts.mjs` must pass unchanged.

## 7. Risks and out of scope

Risks:
- **Concurrency test flakiness.** Barrier interceptors wait with a 20 s timeout; the losing `UPDATE` blocks on the winner's
  row lock until commit (READ COMMITTED), then sees 0 rows → concurrency exception. Assert "exactly one winner", not order.
- **Explicit transaction + retry strategy.** Identity does not enable `EnableRetryOnFailure`; if it is ever enabled, the
  explicit transaction must move into `CreateExecutionStrategy().ExecuteAsync`. Noted in an XML comment.
- **Memory.** Pictures up to 2 MiB load fully; acceptable per SEC-05. `GetInfoAsync` avoids reading bytes.
- **Rollback guard behaviour change.** Rollback now refuses to run against an Identity DB without `ProfilePictures`.
  Documented; rehearsal already migrates to latest.
- **Generated migration drift.** Timestamped filename is produced by the tool; reviewer checks `Up`/`Down` are additive only.
- **Interpretation of AC1** ("extend PlayerProfile") — recorded as an assumption; if reviewers want a navigation on the
  aggregate it is an additive follow-up, not a schema change.
- **Process note.** #137's approval record lacks SuSu's review; flagged in the PR body for the team, not a code blocker.

Out of scope: picture HTTP endpoints and their 404/409/413/415 mappings, `ProfileResponse.picture`, upload validation of
magic bytes at the boundary, cache/`nosniff` headers, contract `x-implemented-by` markers (#144); client display (#145);
cross-device journey (#147); any change to `PlayerProfile` fields or to other services.

Blockers: none.

## 8. Implementation adjustments

Recorded by the Implement stage; behaviour and scope are unchanged.
- **Extra touchpoint:** `src/QuizArena.Identity.Domain/Exceptions/IdentityDomainException.cs` gains a
  `(string message, Exception innerException)` constructor so `ProfilePictureConflictException` and
  `PlayerProfileNotFoundException` can preserve the inner `DbUpdateException` as §3 Step 2 requires. Additive only.
- **Migration file name:** generated by dotnet-ef 10.0.12 as `20261122181426_AddProfilePictures` (+ `.Designer.cs`);
  `Up` is only `CreateTable("ProfilePictures")` (PK, cascade FK, `rowversion`), `Down` only `DropTable`. Snapshot not hand-edited.
- **Store test databases:** `ProfilePictureStoreTests` creates and drops one LocalDB database per test (the existing
  `PlayerProfileRepositoryTests` pattern), not one per class, so failure-injection tests never share rows.
- **Object-write failure shape:** EF wraps an exception thrown by a command interceptor in `DbUpdateException`; the store
  propagates it unchanged (not a mapped SQL error), so the tests assert `DbUpdateException` with the simulated inner exception.
- **Additional tests:** `ReplaceAsync_FirstUploadWhenObjectWriteFails_LeavesNoPicture` and
  `ReplaceAsync_WithInvalidContentType_ThrowsBeforeWriting` (no INSERT/UPDATE issued); the commit-failure test also
  retries on the same store (`...RollsBackToPreviousPictureAndRetrySucceeds`).
- **Script header:** the `Rollback` line in `Invoke-LegacyMigration.ps1`'s comment help notes that a profile with an
  uploaded picture is always kept.

### Repair cycle 1 (review on PR #154)

- **Unknown commit outcome.** When the commit step throws, `ProfilePictureStore.ReplaceAsync` disposes the transaction
  and re-reads the row with a no-tracking query (no caller token). It returns success only when the row carries the
  rowversion produced by this call's write plus the expected version, content type and content SHA-256; it rethrows the
  original exception when the row is still the prior state (prior rowversion, or no row on a first upload); otherwise it
  throws `PlayerProfileNotFoundException` (profile gone) or `ProfilePictureConflictException`. Guarantee: one call
  increments the version at most once and never reports failure for a committed write that is still current, so the
  caller has nothing to retry. Separate calls are not de-duplicated. If the verification read fails, the original
  commit exception is rethrown and the outcome stays unknown (residual risk).
  (Superseded by repair cycle 2: the verification is now a locking read and an inconclusive read throws its own exception.)
- **Validated reads.** `GetAsync` maps through `ProfilePicture.Rehydrate`, `GetInfoAsync` through the new
  `ProfilePicture.RehydrateInfo` (content type, version, UTC; bytes are not loaded so empty content is caught only by
  `GetAsync`), and `ReplaceAsync` validates the loaded row. A corrupt row throws the new
  `ProfilePictureCorruptException` (extra touchpoint, `IdentityDomainException`, inner `InvalidProfileException`); it is
  not mapped to 400, so it reaches the default 500 until #144 maps errors.
- **Concurrency vs. deletion.** A `DbUpdateConcurrencyException` whose profile no longer exists (cascade delete) now
  throws `PlayerProfileNotFoundException` instead of a conflict.
- **Failure cleanup.** Only the picture entry tracked by the failed call is detached; `ChangeTracker.Clear()` is gone.
- **Tests added:** lost-acknowledgement interceptor (`TransactionCommittedAsync` throws after the real commit) for
  replace and first upload, plus lost acknowledgement followed by a competing replace (conflict) or profile delete
  (not found); profile deleted before the UPDATE; unrelated tracked entries kept; corrupt rows inserted with raw SQL for
  reads and replace; domain tests for `RehydrateInfo` and empty content. HTTP 409/404 mapping stays with #144.

### Repair cycle 2 (review on PR #154 at 9c74b0d)

- **Locking verification read.** The commit-outcome check reads the picture through `FromSql` with the
  `WITH (READCOMMITTEDLOCK)` table hint (parameterized `{profileId}`), and the profile-existence check uses the same
  hint through `Database.SqlQuery<int>`. The hint forces locking read-committed even when READ_COMMITTED_SNAPSHOT is
  on, so a commit still being completed (holding its exclusive row lock) is waited for instead of the prior row
  version being read. The repair-cycle-1 RCSI residual no longer applies and was removed from the store remarks.
- **Inconclusive vs. confirmed rollback.** Confirmed rollback (prior rowversion still current, or no row on a first
  upload while the profile exists) rethrows the original commit exception (retry safe). A failed or timed-out
  verification read now throws the new `ProfilePictureCommitOutcomeUnknownException` (extra touchpoint,
  `IdentityDomainException`, inner = original commit exception), documented as "retry may double-count". It is not
  mapped to an HTTP status here (#144).
- **First upload deleted during verification.** With no prior rowversion and no row, the store checks the profile
  first: missing profile throws `PlayerProfileNotFoundException`; profile present means confirmed rollback.
- **Metadata read rejects unreadable rows.** `GetInfoAsync` projects `picture.Content.Length`
  (`CAST(DATALENGTH([p].[Content]) AS int)`, bytes not selected) and `ProfilePicture.RehydrateInfo` now takes the
  stored length and enforces 1..2,097,152, so empty and oversized rows are rejected by both reads.
- **Tests added/changed:** RCSI enabled per test database (`ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON
  WITH ROLLBACK IMMEDIATE` after clearing the pool) for lost-acknowledgement first upload + replace (success, locking
  SQL asserted), genuine commit failure (rethrow) and an in-flight writer before verification (a snapshot read sees
  version 1; the locking read waits and reports a conflict; this test fails if the hint is removed); verification read
  failure (`ProfilePictureCommitOutcomeUnknownException`); first-upload lost acknowledgement with the profile deleted
  (not found); corrupt-row reads now expect both reads to reject empty and oversized content; the metadata read SQL
  contains `DATALENGTH` and no other `[Content]`; domain `RehydrateInfo` length cases.
