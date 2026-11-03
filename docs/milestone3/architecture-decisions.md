# Milestone 3 architecture decisions (P02)

Status: **Proposed — final team approval pending.** Issue [#17](https://github.com/JimRashed/Web-3-Final-Project/issues/17), parent #8, epic #7. These contracts describe the target, not the deployed modular monolith at baseline `4917c48`. [Plan](../../specs/issue-17-plan.md), [API contracts](api-contracts.md), [authorization](authorization-matrix.md), [migration](identity-migration.md), [evidence](p02-validation.md).

## Decisions

| ID | Proposed decision | Reason / consequence |
|---|---|---|
| ADR-01 | Six independent ASP.NET Core .NET 10 services, each Domain/Data/Api triple | Own deployment, dependency graph and SQL Server database; no peer assembly references |
| ADR-02 | Pure Domain owns entities, rules, exceptions and repository abstractions; Data references only its own Domain; Api references its own Data/Domain | Api owns HTTP DTOs, application interfaces/orchestration, mapping and typed HTTP adapters. No EF/HTTP dependencies in Domain |
| ADR-03 | One React TypeScript client, Node build tooling, no Express business API or MongoDB | Browser consumes public HTTP APIs; backend owns persistence |
| ADR-04 | Keycloak handles registration/login/recovery; Identity owns profiles | Authorization code with PKCE S256; no QuizArena password form or token minting |
| ADR-05 | Exact trusted issuer plus opaque subject is identity | One issuer per deployment; subject never parsed as GUID. Internal ownership and uniqueness include issuer |
| ADR-06 | Two players, five rounds, one answer each per round, one point per correct answer | Advance only after both answers; ties have no winner; no real-time transport, matchmaking or catalog administration |
| ADR-07 | HTTP adapters and durable Gameplay outbox | Local atomic transaction + at-least-once deliveries with idempotent receivers; no broker or distributed transaction |
| ADR-08 | Preserve old data and expose explicit transition errors | No destructive migration in P02, no silent account linking, no truncation of historical scores/IDs |
| ADR-09 | Version 1 proposed API contract on dedicated service origins | Existing route names retained where possible; new client switches all service origins together after approved migration. Breaking future changes require a new version/decision and consumer migration |
| ADR-10 | Final human review replaces intermediate dispatcher approval prompts | Documents remain proposals until all three owners approve; automated checks do not supply human signoff |

## Owners and data boundaries

For service `S`, owned production paths are `src/QuizArena.S.{Domain,Data,Api}/`, tests `tests/QuizArena.S.{UnitTests,IntegrationTests}/`, and UI `src/QuizArena.Client/src/features/<lowercase-service>/`. Shared React shell belongs to P07; shared test support must contain no production business/domain contracts.

| Service | Owner | Existing tables transferred to its own database | Additional target state |
|---|---|---|---|
| Identity | CarterBeasse | Users | Profiles with unique (Issuer,Subject); legacy credential hashes archived separately |
| Statistics | CarterBeasse | MatchResults | Unique (Issuer,MatchId,UserId), immutable replay comparison |
| QuestionBank | JimRashed | Questions, Categories | Four options and one matching correct answer; validated seed content |
| Lobby | JimRashed | ActiveLobbies, LobbyPlayers | Unique JoinCode, one host and at most two distinct members |
| Gameplay | SuSu-Lisu | ActiveMatches, PlayerStates | Immutable question snapshots, per-round answers, unique stable LobbyId, outbound deliveries |
| Notification | SuSu-Lisu | EventLogs, UserPrefs | Immutable EventId; unique (Issuer,Subject) preferences |

Each service owns its DbContext, migrations and connection configuration. No cross-service FK, join, repository, shared DbContext or shared production "Common Domain" assembly. Other service identifiers are opaque references. Api adapters deserialize into locally owned DTOs, never reference the peer's project. Constructor DI; DbContext/application services Scoped; background delivery workers create scopes per unit of work. All I/O methods accept and propagate CancellationToken; no sync-over-async. SQL transactions belong to one service only.

## Configuration contract

| Component | Development origin | Resource audience / database |
|---|---|---|
| Client | http://localhost:5173 | public client quizarena-client |
| Keycloak | http://localhost:8080/realms/quizarena | exact issuer; configure deployment HTTPS URL |
| Identity | http://localhost:5101 | quizarena-identity / QuizArenaIdentityDb |
| Statistics | http://localhost:5102 | quizarena-statistics / QuizArenaStatisticsDb |
| QuestionBank | http://localhost:5103 | quizarena-questionbank / QuizArenaQuestionBankDb |
| Lobby | http://localhost:5104 | quizarena-lobby / QuizArenaLobbyDb |
| Gameplay | http://localhost:5105 | quizarena-gameplay / QuizArenaGameplayDb |
| Notification | http://localhost:5106 | quizarena-notification / QuizArenaNotificationDb |

Use per-service `ConnectionStrings:DefaultConnection`, `Authentication:Authority`, `Authentication:Audience`, `Authentication:MachineClientId`, `Cors:AllowedOrigins`. Gameplay additionally configures `ServiceUrls:{Lobby,QuestionBank,Statistics,Notification}` and confidential credentials through environment/user-secrets/CI secrets. React configures public `VITE_<SERVICE>_BASE_URL`, `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID`; these must never hold secrets. Fail startup on missing required non-development configuration. No connection strings or client secrets in this package.

HTTP authority metadata and HTTP APIs are Development-only; deployed HTTPS, exact redirect/logout URI allowlists, CORS only configured browser origins. Swagger only Development. Browser Swagger/client generation excludes all machine operations and their reachable schemas (including answer-bearing snapshots); internal specs are for backend developers. GET /health is public liveness and never discloses topology/secrets.

## Interaction and transaction boundaries

1. Client creates/joins/locks Lobby. Lobby determines host/members from authenticated subject.
2. Client asks Gameplay to create a match from joinCode. Gameplay obtains a machine token for Lobby, verifies Locked state and caller=host, then obtains five immutable QuestionBank snapshots using a QuestionBank-audience machine token.
3. Gameplay atomically inserts match, five snapshots, player/round state and two MATCH_CREATED event delivery records. Unique LobbyId prevents concurrent duplicate matches. No match is persisted if prerequisites fail.
4. Answers update one participant's current-round response under concurrency control. Only the transaction receiving the second distinct answer advances; final round commits FINISHED, original completion timestamp, winner/draw and four outbox rows (two results and two completion events) together.
5. Worker sends each persisted row independently to its destination. Results/events receivers commit deduplication and effects atomically. Notification failure never rolls back a committed match/result.

Delivery row: Id(Guid), destination, operation version1, immutable canonical payload, idempotency key, status Pending/Delivered/Failed, attemptCount, nextAttemptAt UTC, last safe error code, createdAt, deliveredAt nullable. Event IDs are generated once before commit; PlayedAt is the original completion time. Serialize property order consistently for diagnostics, but receivers compare typed field values (not whitespace or JSON member order). Missing optional values normalize according to the API rules.

Proposed worker: claim rows with a persisted 30-second lease; 5-second HTTP timeout; retry network/408/429/5xx, refreshing expired machine tokens once for401 then flag configuration failure. Eight attempts total, exponential delays min(300 seconds, 2^(attempt-1)) plus 0..1 second jitter; Retry-After honored up to300 seconds. Persist next attempt before releasing lease. After restart expired leases become claimable; no reset of payload/IDs/counts. Other4xx and exhausted retries become Failed with visible operational diagnostics, never discarded. Manual operator redrive after cause resolution preserves payload/IDs and starts a recorded new attempt series. No extra public admin endpoint in this milestone. Independent delivery progress prevents one failed destination from blocking the others.

## Version and review gate

OpenAPI version1.0.0 is a proposal snapshot, not a released compatibility promise. Retain baseline documents as historical references. Retired routes return410 throughout Milestone3; removal requires a later decision. New endpoints and string identities require a coordinated frontend/service cutover; no silent dual-write period. Migration is specified separately.

| Reviewer | Owned services | Approval | Evidence/date |
|---|---|---|---|
| JimRashed | QuestionBank, Lobby | Pending | — |
| CarterBeasse | Identity, Statistics | Pending | — |
| SuSu-Lisu | Gameplay, Notification | Pending | — |

All three review cross-service semantics. Independent PR approval must come from someone other than PR author; no agent may populate these as approved.
