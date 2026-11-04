# Issue #39: QuestionBank persistence

## Summary and architecture

Implement [task #39](https://github.com/JimRashed/Web-3-Final-Project/issues/39), parent #11 and epic #7, on the milestone integration branch. Prerequisites #38 and #19 are merged. QuestionBank owns its SQL database, catalog categories, questions and ordered options. EF records stay in Data and map through the immutable validated Domain Question constructor. Domain owns repository abstractions and selection/count decisions; Data owns SQL queries, mappings and migrations. HTTP 409 mapping belongs to the later API task.

## File inventory

- Add `src/QuizArena.QuestionBank.Domain/Repositories/IQuestionRepository.cs`: asynchronous catalog read/write and public up-to/internal exact snapshot operations required by the persistence use cases; cancellation on I/O.
- Add `src/QuizArena.QuestionBank.Data/Entities/CategoryRecord.cs`, `QuestionRecord.cs`, `QuestionOptionRecord.cs`: persistence records with Guid identities, category relation, ordered options and answer; no public API DTOs.
- Add `src/QuizArena.QuestionBank.Data/Configurations/CategoryConfiguration.cs`, `QuestionConfiguration.cs`, `QuestionOptionConfiguration.cs`: separate EF mappings, required bounded fields, keys/indexes, restricted category deletion and question-owned option deletion.
- Add `src/QuizArena.QuestionBank.Data/Seed/QuestionCatalog.cs`: deterministic stable IDs and at least five valid seeded questions/categories/options, applied via model configuration.
- Modify `src/QuizArena.QuestionBank.Data/Context/QuestionBankDbContext.cs`: expose owned Questions, Categories and QuestionOptions sets; retain assembly configuration discovery.
- Add `src/QuizArena.QuestionBank.Data/Repositories/QuestionRepository.cs`: validated mapping, cancellation, AsNoTracking reads and randomized selection without replacement via existing domain rules.
- Modify `src/QuizArena.QuestionBank.Data/QuestionBankPersistenceExtensions.cs`: register the repository with scoped lifetime.
- Add a generated `AddQuestionCatalog` migration and designer in `src/QuizArena.QuestionBank.Data/Migrations/`; update `QuestionBankDbContextModelSnapshot.cs`. Preserve the existing InitialCreate migration unchanged.
- Add `tests/QuizArena.QuestionBank.IntegrationTests/Persistence/QuestionRepositoryTests.cs` and `QuestionConstraintsTests.cs`; modify existing `MigrationTests.cs` only as necessary for owned schema/seed evidence. Use isolated disposable SQL Server databases and fresh contexts.

## Ordered implementation

1. Inspect existing Domain invariants, service-owned context/factory, API contract and Notification persistence conventions. Run baseline restore/build and focused tests; record existing failures separately.
2. Define the minimal Domain repository abstraction. Implement separate Data records/configurations with category name uniqueness, question identity uniqueness, ordered option keys and appropriate option uniqueness. Required lengths match Domain (prompt 1000, category 80, options/answer 300). Use ordinal-compatible option/answer handling; do not let SQL collation reject otherwise valid ordinally distinct options unintentionally.
3. Seed at least five fixed valid questions. Validate seed content through Domain construction; persist deterministic stable IDs and option order. Generate a new migration using the existing context factory, without rewriting the empty initial migration. Verify fresh migration and zero pending model changes.
4. Implement catalog mapping/read/write and selection. Read-only queries use AsNoTracking and eager category/options loading, preserve option order, pass CancellationToken to all EF I/O, and map records through the validated Question constructor. Shuffle eligible questions then use QuestionSelection for distinct up-to-count and exact snapshots; count 1..50 and shortage remain typed domain exceptions. Store only QuestionBank records and do not query another service database.
5. Add meaningful SQL relational tests: deterministic valid seed and schema ownership after migration; fresh-context/restart round trips with ordered options; restricted category deletion; required fields and unique keys; competing inserts/concurrency behavior where applicable; no tracking; cancellation; count bounds; no replacement; short/empty catalogs; exact shortage and immutable snapshot values. Tests use unique generated databases, configured QUIZARENA_TEST_SQLSERVER or LocalDB, and safe cleanup.
6. Run focused service tests, architecture checks and full solution gates. Inspect complete diff for scope, secrets, generated output and outer-layer references. Return actual evidence for read-only review; Dispatcher publishes a draft PR for #39 with #11/#7 links and pending independent human review.

## Acceptance coverage

| Criterion | Evidence |
| --- | --- |
| Context owns Questions, Categories/options and preserves restricted deletion/required fields | Steps 2, 3, 5; separate configurations and relational constraint tests |
| Deterministic catalog of at least five valid questions | Steps 3, 5; stable migration seed and Domain-valid readback |
| Random selection without replacement; public up-to/internal exact shortage | Steps 4, 5; existing domain selection rules and repository tests |
| Owned migrations, fresh database and fresh-context persistence | Steps 3, 5; migration lineage and relational round trips |
| Repository cancellation/no tracking, correct layer ownership | Steps 2, 4, 5, 6; repository tests and architecture suite |
| Plan, actual validation, read-only review, independent human approval | Step 6; draft PR with final human signoff pending |

## Testing and validation

Use xUnit/FluentAssertions and Arrange/Act/Assert with descriptive Method_Scenario_Outcome names. SQL Server relational tests establish constraints; no in-memory substitute or skipped tests. No endpoint/client changes are planned, so frontend checks are not applicable.

From repository root:

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror -m:1
dotnet test tests/QuizArena.QuestionBank.UnitTests/QuizArena.QuestionBank.UnitTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.QuestionBank.IntegrationTests/QuizArena.QuestionBank.IntegrationTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj --no-build --verbosity normal
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build -m:1 --verbosity normal
dotnet test QuizArena.sln --no-build -m:1 --collect:"XPlat Code Coverage"
```

Offline restore may use the populated local NuGet cache with `--source C:\Users\kilda\.nuget\packages --disable-parallel -m:1`; record the exact command. Tests and format may require execution outside the sandbox because it blocks test-host processes/named pipes. A missing SQL runtime is a real blocker. Record exits and discovered/passed/failed/skipped counts; no historical counts are acceptance evidence.

## Risks and exclusions

Preserve immutable Domain models and the initial migration lineage. Avoid EF constructor binding to the immutable options collection by mapping Data records. Do not delete legacy monolith entities, change HTTP endpoints/authorization/DTOs, add client code or invent unrelated persistence semantics. Random order tests assert set/count properties, not a particular permutation. Existing database cleanup must target only the generated test database. This is an agent-reviewed plan under Dispatcher automatic handoffs; independent human review remains pending at the final draft PR.
