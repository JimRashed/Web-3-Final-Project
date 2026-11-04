# Milestone 3 database operations (P04)

Issue [#19](https://github.com/JimRashed/Web-3-Final-Project/issues/19), parent #8, epic #7. Implements the data boundaries in [ADR-01/ADR-02](architecture-decisions.md): each service owns one SQL Server database, its own `DbContext` and its own migration history. No cross-service foreign keys, joins, repositories or shared context.

## Ownership

| Service | Context (`src/QuizArena.<S>.Data/Context/`) | Development database | Legacy tables it will own (moved in D02 #63) | Schema task |
|---|---|---|---|---|
| Identity | `IdentityDbContext` | `QuizArenaIdentityDb` | Users (profiles only; password hashes archived, never copied) | #27 |
| Statistics | `StatisticsDbContext` | `QuizArenaStatisticsDb` | MatchResults | #33 |
| QuestionBank | `QuestionBankDbContext` | `QuizArenaQuestionBankDb` | Questions, Categories | #39 |
| Lobby | `LobbyDbContext` | `QuizArenaLobbyDb` | ActiveLobbies, LobbyPlayers | #45 |
| Gameplay | `GameplayDbContext` | `QuizArenaGameplayDb` | ActiveMatches, PlayerStates | #51 |
| Notification | `NotificationDbContext` | `QuizArenaNotificationDb` | EventLogs, UserPrefs | #57 |

Each context applies every `IEntityTypeConfiguration<T>` in its own assembly (`ApplyConfigurationsFromAssembly`), so a data task only adds a `DbSet`, a configuration class under `Configurations/` and a migration. The baseline `InitialCreate` migration in every service is intentionally empty; it starts the lineage and `__EFMigrationsHistory`.

The legacy `QuizArenaDb` and `QuizArenaContext` are **not touched**. Data transfer, verification and retirement of the aggregate host are D02 #63.

## Legacy constraints the data tasks must preserve

From `src/QuizArena.Data/Context/QuizArenaContext.cs`, adapted to the P02 contracts (string subjects, string match IDs):

| Owner | Constraint to keep or replace |
|---|---|
| Identity | Unique profile identity: replace unique `Username` with unique `(Issuer, Subject)` |
| Statistics | Unique `(MatchId, UserId)` becomes unique `(Issuer, MatchId, UserId)`; index on `UserId` |
| QuestionBank | Question → Category FK with `Restrict` delete; required prompt, correct answer and category name |
| Lobby | Unique `JoinCode`; LobbyPlayers cascade-deleted with their lobby; add unique `(LobbyId, PlayerId)` |
| Gameplay | `MatchId` key (max 100); PlayerStates unique `(MatchId, UserId)` cascade-deleted with the match |
| Notification | Index on `UserId` for events; unique preference per user; add unique `EventId` |

## Configuration

| Setting | Where | Value |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `appsettings.Development.json` per Api | `Server=(localdb)\MSSQLLocalDB;Database=QuizArena<S>Db;Trusted_Connection=True;TrustServerCertificate=True` (Windows integrated auth, no credentials) |
| `ConnectionStrings:DefaultConnection` | `appsettings.json` per Api | blank: startup fails until the environment supplies it |
| Other environments | environment variable `ConnectionStrings__DefaultConnection`, user-secrets or CI secret | never commit passwords or connection strings with credentials |

Startup never creates or migrates the schema (`EnsureCreated` is not used anywhere in the new services). `/health` stays a liveness check and does not touch the database.

## Commands

Tools are pinned in `.config/dotnet-tools.json` (`dotnet-ef` 10.0.12). From the repository root:

```powershell
dotnet tool restore

# Apply one service's migrations to its development database
dotnet tool run dotnet-ef database update --project src/QuizArena.Identity.Data --startup-project src/QuizArena.Identity.Api --context IdentityDbContext

# Add a migration after changing a service's model (data tasks)
dotnet tool run dotnet-ef migrations add <Name> --project src/QuizArena.Statistics.Data --startup-project src/QuizArena.Statistics.Api --context StatisticsDbContext --output-dir Migrations

# All six
foreach ($s in 'Identity','Statistics','QuestionBank','Lobby','Gameplay','Notification') {
  dotnet tool run dotnet-ef database update --project src/QuizArena.$s.Data --startup-project src/QuizArena.$s.Api --context "${s}DbContext"
}
```

`dotnet ef` uses each Data project's design-time factory. It targets the development LocalDB database unless `QUIZARENA_<SERVICE>_CONNECTION` is set (for example `QUIZARENA_QUESTION_BANK_CONNECTION`), so pointing the tools at another server never requires editing files.

## Tests

`tests/QuizArena.<S>.IntegrationTests/Persistence/MigrationTests.cs` in every service:

- `Migrate_OnFreshDatabase_AppliesEveryMigrationAndLeavesNonePending` creates a uniquely named database (`QuizArena<S>_Migration_<guid>`), applies all migrations, checks nothing is pending, then drops it.
- `Model_ComparedWithLatestMigration_HasNoUnmigratedChanges` fails if someone changes a model without adding a migration (verified by adding a probe entity: the test failed).

They use LocalDB by default; set `QUIZARENA_TEST_SQLSERVER` to a server connection string (no database name) to run them elsewhere, e.g. in CI (P10 #25). Shared per-test reset and seeding helpers are P09 #24.

## Dependency note

`Microsoft.EntityFrameworkCore.SqlServer` 10.0.12 pulls `System.Security.Cryptography.Pkcs` 9.0.11 through `Microsoft.Data.SqlClient`, which has two advisories ([GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7), [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx)). Every Data project, including legacy `QuizArena.Data`, pins the patched `System.Security.Cryptography.Pkcs` 10.0.13. With that, `dotnet build QuizArena.sln -warnaserror` passes with 0 warnings. Remove the pin once SqlClient depends on a patched version.

## Verification (2026-11-03, LocalDB 17.0, .NET SDK 10.0.401)

- `dotnet build QuizArena.sln --no-restore -warnaserror`: 0 warnings, 0 errors.
- `dotnet test QuizArena.sln`: all passed, including 12 migration tests (2 per service); no test databases left behind.
- `dotnet ef database update` for all six contexts: each created its own `QuizArena<S>Db` containing only its own `__EFMigrationsHistory` row.
