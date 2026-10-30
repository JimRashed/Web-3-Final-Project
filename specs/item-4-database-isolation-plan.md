# Fix integration-test database isolation (#4)

## Summary & Architecture
Replace the current collection-wide, one-time database state with an explicit reset-and-seed boundary for every integration test instance. The existing `QuizArenaWebApplicationFactory` remains the shared host/fixture, but each test class will derive from a small integration-test base that resets the database before its test instance runs, preserving only deterministic seed data and preventing mutations from leaking between tests.

## File Inventory
- Modify `tests/QuizArena.IntegrationTests/Infrastructure/QuizArenaWebApplicationFactory.cs` — extract deterministic seeding into a reusable reset operation and make reset safe for each test.
- Modify `tests/QuizArena.IntegrationTests/Infrastructure/IntegrationTestCollection.cs` — retain the shared host fixture while aligning with the isolation boundary.
- Create `tests/QuizArena.IntegrationTests/Infrastructure/IntegrationTestBase.cs` — invoke the factory reset during xUnit test-instance initialization and expose the factory to endpoint tests.
- Modify `tests/QuizArena.IntegrationTests/Endpoints/AuthEndpointsTests.cs` — use the isolation base.
- Modify `tests/QuizArena.IntegrationTests/Endpoints/LobbyEndpointsTests.cs` — use the isolation base.
- Modify `tests/QuizArena.IntegrationTests/Endpoints/MatchEndpointsTests.cs` — use the isolation base.
- Modify `tests/QuizArena.IntegrationTests/Endpoints/QuestionEndpointsTests.cs` — use the isolation base.
- Modify `tests/QuizArena.IntegrationTests/Endpoints/NotificationEndpointsTests.cs` — use the isolation base.
- Modify `tests/QuizArena.IntegrationTests/Endpoints/StatisticsEndpointsTests.cs` — use the isolation base.

## Step-by-Step Task Sequence
1. Add the reusable integration-test base and define the per-test reset lifecycle.
2. Refactor the WebApplicationFactory database setup so reset deletes/recreates the unique LocalDB database and seeds only the canonical category, question, and lobby data.
3. Migrate all endpoint test classes to the isolation base without changing their endpoint assertions or production behavior.
4. Build the integration-test project and run the integration tests to verify the reset lifecycle and existing scenarios.
5. Run format verification and the full solution test suite; inspect the diff for unintended changes.

## Testing Strategy
- Unit tests: no production/domain behavior changes are planned for this isolation-only change.
- Integration tests: run all existing `QuizArena.IntegrationTests` scenarios, including registration, lobby mutation, match persistence, notifications, questions, and statistics. The reset hook must ensure each test starts with the canonical seed state and that tests remain independent when run in the test runner's normal order.
- Isolation verification: confirm tests that mutate users, lobbies, matches, events, or results do not rely on prior test execution and that the seed lobby/question remain available after every reset.

## Validation Commands
- `dotnet build QuizArena.sln --no-restore`
- `dotnet test tests/QuizArena.IntegrationTests/QuizArena.IntegrationTests.csproj --no-build --verbosity normal`
- `dotnet format QuizArena.sln --verify-no-changes --no-restore`
- `dotnet test QuizArena.sln --no-restore --verbosity normal`
