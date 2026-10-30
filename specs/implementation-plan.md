# QuizArena Issue #4 Test Coverage Plan

## Summary & Architecture

Add two .NET 10 xUnit test projects under `tests/`: `QuizArena.UnitTests` for isolated service and repository behavior, and `QuizArena.IntegrationTests` for HTTP endpoint and SQL Server LocalDB persistence coverage. Both projects will reference the relevant production projects, use FluentAssertions and Moq for Arrange-Act-Assert unit tests, and run from the solution through `dotnet test`.

Integration tests will use a custom `WebApplicationFactory<Program>` with a unique LocalDB database per test run, override the production connection string and JWT key with test-only values, initialize the schema, and isolate mutable state. Authentication tests will document the current unauthenticated behavior only; no JWT enforcement or `[Authorize]` production changes are planned.

## File Inventory

### Files to create

- `tests/QuizArena.UnitTests/QuizArena.UnitTests.csproj`
- `tests/QuizArena.UnitTests/Services/AuthServiceTests.cs`
- `tests/QuizArena.UnitTests/Services/QuestionServiceTests.cs`
- `tests/QuizArena.UnitTests/Services/LobbyServiceTests.cs`
- `tests/QuizArena.UnitTests/Services/GameplayServiceTests.cs`
- `tests/QuizArena.UnitTests/Services/StatisticsServiceTests.cs`
- `tests/QuizArena.UnitTests/Services/NotificationServiceTests.cs`
- `tests/QuizArena.UnitTests/Repositories/RepositoryTests.cs`
- `tests/QuizArena.IntegrationTests/QuizArena.IntegrationTests.csproj`
- `tests/QuizArena.IntegrationTests/Infrastructure/QuizArenaWebApplicationFactory.cs`
- `tests/QuizArena.IntegrationTests/Infrastructure/IntegrationTestCollection.cs`
- `tests/QuizArena.IntegrationTests/Endpoints/AuthEndpointsTests.cs`
- `tests/QuizArena.IntegrationTests/Endpoints/QuestionEndpointsTests.cs`
- `tests/QuizArena.IntegrationTests/Endpoints/LobbyEndpointsTests.cs`
- `tests/QuizArena.IntegrationTests/Endpoints/MatchEndpointsTests.cs`
- `tests/QuizArena.IntegrationTests/Endpoints/StatisticsEndpointsTests.cs`
- `tests/QuizArena.IntegrationTests/Endpoints/NotificationEndpointsTests.cs`

### Files to modify

- `QuizArena.sln` — add both test projects under a `tests` solution folder and include build configurations.
- `src/QuizArena.Api/Program.cs` — expose the top-level `Program` type for `WebApplicationFactory` while preserving current production behavior.
- `src/QuizArena.Api/QuizArena.Api.csproj` — modify only if required for test-host accessibility or test assets.

### Files not planned for modification

Domain contracts, entities, services, repositories, contexts, and controllers will remain unchanged unless a minimal production change is proven necessary for compilation or testability. Production connection strings and credentials will not be modified.

## Step-by-Step Task Sequence

1. Create the unit test project and package configuration with xUnit, FluentAssertions, Moq, the test SDK, coverlet, and references to Domain, Data, and Api.
2. Create the integration test project with xUnit, FluentAssertions, ASP.NET Core test-host dependencies, EF Core SQL Server support, and references to Api and Data.
3. Add both test projects to `QuizArena.sln`, create the `tests` solution folder, and add the minimal `Program` accessibility change required by `WebApplicationFactory`.
4. Implement the LocalDB integration fixture with unique database naming, test-only configuration overrides, schema initialization, deterministic seed data, and cleanup.
5. Add Identity and QuestionBank unit coverage for registration, duplicate users, login outcomes, user lookup, DTO mapping, count/cancellation forwarding, and dependency failures.
6. Add Lobby and Gameplay unit coverage for lobby locking, match creation, notifications, question retrieval, scoring, completion, missing matches, and persistence failures.
7. Add Statistics and Notification unit coverage for result mapping, aggregates, leaderboard behavior, event mapping/order, preference upserts, and dependency failures.
8. Add repository tests against isolated LocalDB for user, question, lobby, match, event, preference, and statistics persistence behavior, including relational constraints and duplicate-key failures.
9. Add HTTP integration coverage for every current controller route, including success responses, validation failures, conflicts, not-found responses, persistence verification, and current unauthenticated behavior.
10. Run restore, build, format, unit tests, integration tests, and the full solution test suite; resolve warnings, discovery issues, cleanup failures, and nondeterministic behavior.

## Testing Strategy

### Unit Tests

Use xUnit, FluentAssertions, and Moq with Arrange-Act-Assert structure. Target `AuthService`, `QuestionService`, `LobbyService`, `GameplayService`, `StatisticsService`, and `NotificationService`. Verify repository interactions, cancellation tokens where supported, DTO mappings, aggregate calculations, scoring/completion behavior, and exception propagation. Repository tests will use a real `QuizArenaContext` against isolated SQL Server LocalDB databases rather than mocked EF queryables.

### Integration Tests

Use `WebApplicationFactory<Program>` and `HttpClient` against the ASP.NET Core pipeline and SQL Server LocalDB.

- Identity: registration, duplicate registration (`409`), login success/invalid credentials (`401`), user lookup success/not found (`404`).
- QuestionBank: generation and invalid count (`400`).
- Lobby: existing and missing join codes (`404`), plus persisted lock state.
- Gameplay: match creation (`201`), retrieval/not found (`404`), next question, answer submission, and persisted progression.
- Statistics: result recording, player statistics, history, leaderboard, and empty data behavior.
- Notification: event creation/retrieval and preference creation/update.
- Authentication: document that routes are currently accessible without authorization enforcement; do not introduce authentication middleware as part of this issue.

Use only generated LocalDB database names and test JWT configuration. LocalDB remains an environment prerequisite and must be documented if CI does not provide it.

## Validation Commands

Run from the repository root:

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore
dotnet format QuizArena.sln --verify-no-changes
dotnet test QuizArena.sln --no-build --verbosity normal
```

Focused execution during implementation:

```powershell
dotnet test tests\QuizArena.UnitTests\QuizArena.UnitTests.csproj --no-restore --verbosity normal
dotnet test tests\QuizArena.IntegrationTests\QuizArena.IntegrationTests.csproj --no-restore --verbosity normal
```

Before implementation begins, confirm that the development and CI environments provide SQL Server LocalDB. If they do not, decide whether the integration suite should fail fast with a clear prerequisite message or use a test-only fallback.