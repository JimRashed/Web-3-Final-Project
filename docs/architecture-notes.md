# Architecture Notes

## 1. Architectural Style

QuizArena is deployed and executed as one ASP.NET Core API, but its source code is organized into explicit business modules. This approach provides the deployment simplicity of a single application while preserving logical boundaries that can evolve independently.

The solution is divided into three primary projects:

- `QuizArena.Api` contains the HTTP delivery layer, controllers, application services, dependency-injection composition, and API-specific behavior.
- `QuizArena.Domain` contains business contracts, domain entities, and service interfaces. It does not depend on the API or data-access implementation.
- `QuizArena.Data` contains Entity Framework Core contexts and repository implementations. It provides persistence behind repository interfaces consumed by the API services.

Within the domain project, capabilities are separated through namespaces such as `QuizArena.Domain.Identity`, `QuizArena.Domain.Statistics`, `QuizArena.Domain.QuestionBank`, `QuizArena.Domain.Lobby`, `QuizArena.Domain.Gameplay`, and `QuizArena.Domain.Notification`. These namespaces establish ownership of entities and contracts without requiring each capability to be a separate deployable application.

The application layer depends on abstractions rather than concrete persistence details. Services consume interfaces such as `IAuthService`, `IGameplayService`, `IStatisticsService`, and `INotificationService`. API controllers depend on those service contracts, while services depend on repository contracts such as `IUserRepository`, `IMatchRepository`, and `IStatisticsRepository`. Concrete repository implementations are registered by the composition root in `Program.cs`.

This arrangement preserves the dependency direction:

```text
HTTP Controllers
	   |
	   v
Application Services and Domain Service Contracts
	   |
	   v
Repository Interfaces
	   |
	   v
EF Core Repository Implementations and DbContext
	   |
	   v
SQL Server LocalDB
```

The interfaces provide stable seams between layers. Controllers do not query EF Core directly, and services do not need to know whether data is stored in SQL Server, another relational database, or a test double. Repositories encapsulate persistence concerns such as LINQ queries, relationship loading, entity updates, and `SaveChangesAsync` calls.

The structure also provides a practical boundary for team development. Each team can work primarily within its domain's entities, contracts, services, repositories, and controllers. Shared concerns remain explicit rather than being spread through unstructured global collections or direct cross-module database access.

## 2. Bounded Context Justification

The six domains represent distinct business capabilities. Each domain owns a specific type of business decision and exposes only the contracts needed by other parts of the application.

### Identity

The Identity domain owns user identity and authentication-related behavior:

- Registering users.
- Enforcing username uniqueness through the persistence model.
- Hashing and verifying passwords.
- Looking up users by identifier or username.
- Creating login responses and tokens.

Identity does not own matches, scores, questions, lobby membership, or notification preferences. Other domains refer to users through identifiers and contracts rather than duplicating authentication logic.

Primary elements include:

- `User` entity.
- `RegisterRequest`, `LoginRequest`, `LoginResponse`, and `UserDto` contracts.
- `IAuthService` and `IUserRepository` interfaces.
- `AuthService` and `UserRepository` implementations.

### Statistics

The Statistics domain owns historical match outcomes and derived player metrics:

- Recording match results.
- Calculating games played, wins, and total score.
- Returning player match history.
- Producing leaderboard entries.

Statistics does not manage the live state of a match. It stores the result of a match after gameplay has occurred and calculates reporting views from those results. This separates operational gameplay state from historical analytics data.

Primary elements include:

- `MatchResult` entity.
- `RecordResultRequest`, `PlayerStatsDto`, `MatchHistoryDto`, and `LeaderboardEntryDto` contracts.
- `IStatisticsService` and `IStatisticsRepository` interfaces.
- `StatisticsService` and `StatisticsRepository` implementations.

### Question Bank

The Question Bank domain owns the question catalog and question categorization:

- Selecting questions for quiz sessions.
- Loading questions with their categories.
- Returning question presentation data without exposing answer storage details through the public DTO.

Question Bank does not manage users, live match scores, lobby membership, or event history. Its responsibility is the question content used by quiz functionality.

Primary elements include:

- `Question` and `Category` entities.
- `QuestionDto` contract.
- `IQuestionService` and `IQuestionRepository` interfaces.
- `QuestionService` and `QuestionRepository` implementations.

The current Gameplay implementation still contains a static mock-question collection for its next-question operation. That is an implementation limitation rather than a change to the intended boundary. A later integration can replace that collection with a call to the Question Bank service contract.

### Lobby

The Lobby domain owns the pre-match waiting area and lobby state transitions:

- Finding a lobby by join code.
- Tracking lobby players.
- Locking a lobby before gameplay begins.
- Returning the lobby identifier, status, join code, and player identifiers.

Lobby does not create or score matches. Its responsibility ends when the group is provisioned and the lobby transitions to a locked state.

Primary elements include:

- `ActiveLobby` and `LobbyPlayer` entities.
- `LobbyCreateRequest` and `LobbyProvisionResponse` contracts.
- `ILobbyService` and `ILobbyRepository` interfaces.
- `LobbyService` and `LobbyRepository` implementations.

### Gameplay Engine

The Gameplay Engine owns active match execution:

- Creating active matches.
- Loading live match state.
- Selecting the current question for a match.
- Validating submitted answers.
- Updating player scores.
- Advancing the question index.
- Marking a match as finished.

Gameplay does not own user registration, permanent statistics reporting, lobby provisioning, or notification preference management. It uses player identifiers and match data as gameplay inputs and emits notification events when significant gameplay milestones occur.

Primary elements include:

- `ActiveMatch` and `PlayerState` entities.
- `CreateMatchRequest`, `MatchResponse`, `QuestionResponse`, `SubmitAnswerRequest`, and `SubmitAnswerResponse` contracts.
- `IGameplayService` and `IMatchRepository` interfaces.
- `GameplayService` and `MatchRepository` implementations.

The boundary between live gameplay and Statistics is intentional: live scores and match lifecycle belong to Gameplay, while durable result reporting and aggregation belong to Statistics. The current API exposes result recording as a separate operation.

### Notifications

The Notifications domain owns user-facing event records and notification preferences:

- Recording events such as `MATCH_CREATED` and `MATCH_COMPLETED`.
- Reading a user's event history.
- Creating or updating email and push-notification preferences.

Notifications does not decide when a match is created or completed. Gameplay makes that business decision and invokes the notification service contract to record the resulting event. This keeps event persistence separate from gameplay rules.

Primary elements include:

- `EventLog` and `UserPref` entities.
- `CreateEventRequest`, `EventResponse`, `UpdatePrefRequest`, and `UserPrefDto` contracts.
- `INotificationService` and `IEventRepository` interfaces.
- `NotificationService` and `EventRepository` implementations.

## 3. Communication Strategy

QuizArena uses two communication mechanisms: external HTTP communication at the application boundary and in-process dependency-injected contracts.

### External HTTP communication

Web clients communicate with the API through ASP.NET Core controllers. Controllers define the public transport contract, including HTTP methods, routes, request binding, response status codes, and DTO serialization. Examples include:

- `/api/auth/register` and `/api/auth/login` for Identity.
- `/api/questions/generate` for Question Bank.
- `/api/lobbies/{joinCode}/lock` for Lobby.
- `/matches` and `/matches/{matchId}/answers` for Gameplay.
- `/api/results` and `/api/leaderboard` for Statistics.
- `/events` and `/users/{userId}/preferences` for Notifications.

Controllers do not contain persistence logic. They translate HTTP requests into service calls and translate service results into HTTP responses such as `200 OK`, `201 Created`, `202 Accepted`, `404 Not Found`, `409 Conflict`, and `401 Unauthorized`.

### In-process dependency-injected communication

Within the solution, domain capabilities communicate through interfaces registered with ASP.NET Core dependency injection. This is a direct in-process call rather than an HTTP request. For example:

```text
MatchesController
	  |
	  v
IGameplayService
	  |
	  +--> IMatchRepository
	  |
	  +--> INotificationService
```

`GameplayService` uses `INotificationService` to record `MATCH_CREATED` and `MATCH_COMPLETED` events. The caller depends on the interface, not on the concrete `NotificationService` implementation. This keeps the dependency explicit and makes it possible to replace the implementation with an adapter later.

Repositories use the same strategy. Services depend on repository interfaces, while `Program.cs` maps those interfaces to EF Core implementations:

```text
IUserRepository       -> UserRepository
IMatchRepository      -> MatchRepository
IQuestionRepository   -> QuestionRepository
ILobbyRepository      -> LobbyRepository
IStatisticsRepository -> StatisticsRepository
IEventRepository      -> EventRepository
```

### Preparation for distributed services

The current design creates a migration path to distributed services without prematurely introducing network complexity:

1. A domain service interface can become a remote client interface.
2. The current in-process implementation can be replaced by an HTTP, messaging, or gRPC adapter.
3. DTOs already define explicit data exchanged at the application boundary.
4. Controllers do not need to change if the service abstraction and response behavior remain stable.
5. Repository interfaces can be replaced by a domain-owned persistence adapter if a module receives its own database.
6. Events already represent a natural integration boundary for asynchronous messaging.

For example, the current `GameplayService -> INotificationService` call could later be implemented as an event publication to a message broker. The Gameplay domain would publish a `MatchCreated` or `MatchCompleted` integration event, and Notifications would consume it independently.

This transition is not automatic. Distributed deployment would require additional decisions around authentication propagation, retries, timeouts, idempotency, eventual consistency, schema ownership, observability, and event versioning. The current interface boundaries reduce the amount of application code that would need to change when those decisions are made.

## 4. Data Persistence Strategy

### Single database and unified DbContext

The application currently uses one SQL Server LocalDB database, `QuizArenaDb`, accessed through a unified `QuizArenaContext`. The unified context exposes the entity sets required by the six business capabilities, including:

- `Users`.
- `Questions` and `Categories`.
- `ActiveLobbies` and `LobbyPlayers`.
- `ActiveMatches` and `PlayerStates`.
- `MatchResults`.
- `EventLogs` and `UserPrefs`.

All repositories receive `QuizArenaContext` through dependency injection and use asynchronous EF Core operations for querying and persistence. The API registers the context with the configured `DefaultConnection` and the SQL Server provider.

The unified context is a deliberate choice. It gives the application one transaction and migration boundary while retaining logical ownership in repositories and domain namespaces. A request can update related data without crossing a network boundary or coordinating distributed transactions.

### EF Core isolation and model configuration

Logical separation is preserved through explicit EF Core configuration rather than separate physical databases. `OnModelCreating` defines each entity's keys, required properties, indexes, and relationships. Examples include:

- Questions reference Categories through `CategoryId`.
- Active Lobbies contain Lobby Players through the lobby relationship.
- Player States reference Active Matches through `MatchId`.
- Usernames have a unique index.
- Lobby join codes have a unique index.
- User preferences have a unique index on `UserId`.
- Match results have an index on `UserId` and a unique composite index for `MatchId` and `UserId`.
- Event logs have an index on `UserId`.

These configurations prevent repository code from having to duplicate schema rules and provide database-level enforcement for important invariants. Each repository still limits access to the entity sets associated with its capability.

The current domain model contains several identifier formats—for example, Identity uses `Guid` user identifiers while some Gameplay and Notification records use string identifiers. Because those types are not universally compatible, the current model does not create foreign-key relationships for every conceptual relationship between domains. The identifiers remain application-level references until the domain contracts are normalized.

### Benefits

The single-database strategy provides several practical benefits:

- Simple local development and deployment.
- A single connection string and migration workflow.
- Straightforward reporting queries across stored data.
- Strong transactional consistency within a request.
- Lower operational cost than multiple database servers.
- Easy use of EF Core migrations.
- Centralized backup, restore, and schema management.

### Trade-offs

The same choice introduces constraints:

- Domains do not have complete physical data ownership because they share one database.
- A schema or migration change can affect the entire application.
- Independent domain scaling is not possible while all capabilities run in one process and share one persistence boundary.
- A high-volume workload in one domain can compete for database resources with other domains.
- Cross-domain queries can create coupling if repositories bypass their domain boundaries.
- Splitting a domain into an independent service later will require data extraction, ownership decisions, synchronization, and migration of foreign-key or identifier relationships.

The architecture therefore favors consistency and development simplicity over maximum isolation. It is appropriate for the current milestone because it preserves clear code boundaries while avoiding the operational overhead of distributed transactions and multiple databases.

### Evolution path

If the system later requires independent deployment or scaling, the persistence model can evolve incrementally:

1. Assign physical table ownership to one domain at a time.
2. Stop other domains from querying those tables directly.
3. Introduce integration events for cross-domain data needs.
4. Replace shared-context repository implementations with domain-specific contexts.
5. Move a domain's database behind its service boundary.
6. Introduce read models or reporting projections where cross-domain queries are still required.

This staged approach preserves the current development model while allowing the system to move toward stronger bounded-context isolation when scale, team ownership, or deployment requirements justify it.
