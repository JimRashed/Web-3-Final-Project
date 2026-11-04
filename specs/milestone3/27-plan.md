# Issue #27 (S01-T02) plan: Identity EF mapping, migration and repository

Parent F01 #9, epic #7. Prerequisites #26 (PlayerProfile, merged in #76) and P04 #19 (contexts, merged in #78).

## Files
| Path | Change |
|---|---|
| src/QuizArena.Identity.Domain/Repositories/IPlayerProfileRepository.cs | Persistence port (Domain owns abstractions, ADR-02) |
| src/QuizArena.Identity.Data/Configurations/PlayerProfileConfiguration.cs | `UserProfiles` table, unique (Issuer, Subject), lengths from the entity, binary collation |
| src/QuizArena.Identity.Data/Configurations/UtcConverter.cs | Read datetime2 back as UTC |
| src/QuizArena.Identity.Data/Repositories/PlayerProfileRepository.cs | Find (no tracking), AddIfAbsent (race-safe), Rename |
| src/QuizArena.Identity.Data/Context/IdentityDbContext.cs, IdentityPersistenceExtensions.cs | DbSet and scoped repository registration |
| src/QuizArena.Identity.Data/Migrations/*_AddUserProfiles.cs | Migration |
| tests/QuizArena.Identity.IntegrationTests/Persistence/PlayerProfileRepositoryTests.cs | Relational tests on a per-test SQL Server database |

## Decisions
- Issuer and Subject use `Latin1_General_100_BIN2`: SQL Server's default collation is case-insensitive, which would merge distinct opaque subjects.
- Provisioning is idempotent under concurrency by relying on the unique index and recovering from duplicate-key errors (2601/2627), not on a check-then-insert alone.
- Legacy `Users` rows and password hashes move only through the D02 #63 mapping ledger; nothing is copied here.

## Validation
`dotnet test tests/QuizArena.Identity.IntegrationTests` (includes migration + drift guard), mutation checks for the concurrency recovery and the collation, `dotnet build -warnaserror`, `dotnet format --verify-no-changes`.
