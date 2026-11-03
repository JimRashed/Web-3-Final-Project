# Issue #19 (P04) plan: service-owned EF configuration, migrations and database boundaries

Parent #8 (F00), epic #7. Prerequisites P02 #17 and P03 #18 (merged). Taken over from @JimRashed by @CarterBeasse.

## Goal
Every service has its own DbContext, SQL Server database, migration lineage and connection configuration, with no shared context and the legacy QuizArenaDb untouched, so each data task (#27, #33, #39, #45, #51, #57) only adds its entities.

## Files (per service S)
| Path | Change |
|---|---|
| src/QuizArena.S.Data/Context/SDbContext.cs | Context applying configurations from its own assembly |
| src/QuizArena.S.Data/Context/SDesignTimeDbContextFactory.cs | For dotnet ef; LocalDB default, env override |
| src/QuizArena.S.Data/SPersistenceExtensions.cs | `AddSPersistence(connectionString)`, fails fast when blank |
| src/QuizArena.S.Data/Migrations/*_InitialCreate*.cs | Empty baseline migration and snapshot |
| src/QuizArena.S.Data/*.csproj | EF SqlServer 10.0.12; patched Pkcs 10.0.13 pin |
| src/QuizArena.S.Api/*.csproj, Program.cs, appsettings*.json | EF Design (private), registration, connection strings |
| tests/QuizArena.S.IntegrationTests/Persistence/MigrationTests.cs | Fresh-database migration test and model-drift guard |

Repo-wide: `.config/dotnet-tools.json` (dotnet-ef 10.0.12), docs/milestone3/database-operations.md, Pkcs pin in legacy src/QuizArena.Data.

## Decisions
- Database names follow the merged P02 configuration contract (`QuizArena<S>Db`) rather than the issue's earlier `QuizArena_<Service>` wording.
- Baseline migrations are empty; service-specific schema belongs to the data tasks (issue AC).
- No EnsureCreated or startup migration; explicit `dotnet ef database update` per service.

## Validation
- `dotnet build QuizArena.sln --no-restore -warnaserror`
- `dotnet test QuizArena.sln --no-build`
- Mutation: add an unmigrated entity, confirm the drift guard fails
- `dotnet ef database update` for each context; inspect databases and history

## Out of scope
Entity mappings (data tasks), legacy data transfer and aggregate retirement (D02 #63), shared test fixtures (P09 #24), CI database provisioning (P10 #25).
