# Issue #47: Lobby REST endpoints and authorization

## Summary and architecture

Issue [#47](https://github.com/JimRashed/Web-3-Final-Project/issues/47), S04-T04, is part of parent [#12](https://github.com/JimRashed/Web-3-Final-Project/issues/12), one branch and one final PR. Prime passed; #44–46 passed their real stage loops on this branch and foundation #21 is complete. This is an agent-reviewed plan with final human review pending.

Expose the committed Lobby routes using thin owned Api controllers and the accepted `ILobbyService`. Preserve accepted Domain, Data and application behavior. Authentication/scope/actor policies run before syntax and lookup; Domain retains host-before-state authorization. Keep the existing anonymous `/health`, real JwtBearer middleware and challenge headers. No match creation, Gameplay client, frontend or shared-fixture work is included.

## Exact file inventory

Create:

- `src/QuizArena.Lobby.Api/Controllers/LobbiesController.cs`: create, preview, join and lock routes, policy attributes and nullable-result mapping.
- `src/QuizArena.Lobby.Api/Controllers/InternalLobbiesController.cs`: machine-only verification route.
- `src/QuizArena.Lobby.Api/Errors/LobbyProblems.cs`: central fixed safe problem construction/customization for framework, controller, authentication and exception errors.
- `src/QuizArena.Lobby.Api/Errors/LobbyExceptionHandler.cs`: owned `IExceptionHandler` translating known failures while preserving cancellation.
- `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyApiFixture.cs`: real secured app against a uniquely named migrated disposable Lobby database.
- `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyLifecycleTests.cs`: actual two-subject lifecycle, Location, read safety, idempotency and fresh-context verification.
- `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyInputTests.cs`: codes, body requirements, identity spoof attempts and authorization-before-syntax.
- `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyRouteSecurityTests.cs`: signed-token matrix on the five actual routes.
- `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyErrorTests.cs`: exact safe problem shapes, failure translation and cancellation behavior.
- `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyContractTests.cs`: exact public/internal JSON and implemented route/policy/body/Location agreement with committed OpenAPI.

Modify:

- `src/QuizArena.Lobby.Api/Program.cs`: register central problem customization/exception handler and safe MVC invalid-model-state response; keep middleware ordering, accepted registrations and health behavior.
- `tests/QuizArena.Lobby.IntegrationTests/Security/SecuredApiFactory.cs`: preserve its public parameterless use by existing tests; add a narrow internal connection-configured constructor/option for endpoint fixtures. Preserve actual JwtBearer and static local OIDC metadata/signing keys. No replacement authentication scheme or blanket authorization handler.
- `tests/QuizArena.Lobby.IntegrationTests/Persistence/LobbyDatabase.cs`: expose its owned connection string internally for fixture configuration, without logging it; preserve unique catalog migration/cleanup semantics.
- `contracts/openapi/lobby.yaml`: preserve routes, policies, DTOs, status/body requirements and examples; make only necessary accurate description/example/response updates discovered when comparing real behavior. Do not require a lock requestBody.

No deletions, packages/project-reference changes or accepted application/Domain/Data edits. Existing security/health tests remain intact. If a cohesive helper filename is needed, update this scoped inventory without expanding behavior.

## Ordered steps

1. **Centralize safe problems.** Use the existing `AddProblemDetails` path with one customization method and central constructor producing exactly `type`, `title`, `status`, `detail`, `traceId`. Clear extra extensions and omit instance/errors/exception/stack fields. Use fixed per-status safe descriptions, a stable absolute problem type URI, and the current Activity/HttpContext trace identifier (nonblank, bounded). Never serialize exception messages, parser/model-state errors, SQL details, request bodies, tokens or credentials. Configure `ApiBehaviorOptions.InvalidModelStateResponseFactory` to return the central 400 problem rather than default ValidationProblemDetails. Apply customization to authentication 401/403 and status-code pages too, preserving existing WWW-Authenticate values. Completion: controller, binding, domain, auth and framework error responses share safe `application/problem+json` and the exact five-key shape.
2. **Map exceptions without changing application behavior.** `InvalidLobbyException` maps 400, `LobbyForbiddenException` 403, `LobbyConflictException` 409 and `UnauthorizedAccessException` 401 with a Bearer challenge. Exhausted `LobbyPersistenceException` and recognized database connectivity/time-out failures map safe 503. Classify actual SqlClient connectivity/timeout failures explicitly; do not turn every programming/constraint exception into dependency unavailable. Unexpected exceptions map safe 500. Do not change the thrown Domain/Data/application types or expose their messages. An `OperationCanceledException` associated with request cancellation propagates; do not convert disconnected callers to 500, start a new response after headers have started or suppress cancellation as success. Completion: actual HTTP failure tests exercise each mapping through the accepted middleware, including exceptions containing sentinel secrets that must be absent from JSON.
3. **Implement the exact routes.** Use `[ApiController]`, constructor-injected `ILobbyService` and literal route templates with unconstrained `{joinCode}`. No regex/length constraint may convert malformed code into route 404. Actions pass `User`, code and request CancellationToken; no controller business decision or raw repository/EF access. Null service results become the central safe 404.

   | Method and route | Policy/actor | Result |
   |---|---|---|
   | POST `/api/lobbies` with required `{}` | `lobbies.write`, user | 201 LobbyResponse, relative Location `/api/lobbies/{returnedCode}` |
   | GET `/api/lobbies/{joinCode}` | `lobbies.read`, user | 200 sanitized LobbyResponse |
   | POST `/api/lobbies/{joinCode}/join` with required `{}` | `lobbies.write`, user | 200 LobbyResponse |
   | POST `/api/lobbies/{joinCode}/lock` | `lobbies.write`, user plus existing Domain host rule | 200 LobbyResponse |
   | GET `/internal/lobbies/{joinCode}` | `lobbies.verify`, machine | 200 LobbyProvisionResponse |

   Use the accepted EmptyLobbyRequest for required create/join bodies so extra hostId/playerId/userId or arbitrary members are rejected. Missing body, JSON null, malformed JSON, scalar and array input are invalid for those required objects. Lock has no required body in OpenAPI: accept bodyless and optionally `{}` using optional empty-body binding; a supplied object still uses the strict EmptyLobbyRequest. Reject supplied JSON null and non-object/unknown-member bodies rather than using any identity field. Distinguish absent transport body from a supplied null using `IHttpRequestBodyDetectionFeature.CanHaveBody` when necessary; do not implement general body parsing inside controllers. Query/header identities are ignored and can never override User. Completion: authorized invalid code reaches application validation 400; valid missing code is 404; create's Location can be followed with a read-scoped token and resolves the same lobby.
4. **Connect a real disposable database test host.** Reuse SecuredApiFactory's actual pipeline and signed RSA token helpers. Configure only the endpoint fixture's owned database connection before app registration; use LobbyDatabase to migrate/create a unique catalog and delete that exact catalog in async cleanup. Existing probe-only factory users need no database work. Each endpoint test creates its own lobby; no execution-order dependence or reuse of a development database. Actual lifecycle tests use real repository and application services. Failure-only tests may replace ILobbyService with a deterministic throwing implementation while retaining the real authentication/authorization/middleware, avoiding unavailable external services and new packages. Completion: actual HTTP state is visible from an independent newly created SQL context.
5. **Exercise lifecycle, input and actual-route security.** Implement the cases below using TestTokens with distinct trusted subjects, existing exact scope/client/kind rules and actual HTTP verbs/bodies. Keep preexisting probe matrix tests; add actual-route coverage rather than changing probes to pretend they are controllers. Ensure protected endpoints authenticate/authorize before syntax, and preserve nonhost-before-state behavior through Domain. Completion: every route has actual success, 401 and 403 evidence; resource/state/input cases and persisted outcomes agree with the contract.
6. **Verify OpenAPI and run gates.** Compare all five routes, actor/policy metadata, required create/join bodies, bodyless lock, safe response keys, Location and errors against committed OpenAPI. Preserve the existing public contract and run the repository's offline checker. Update only an inaccurate relevant detail, never weaken policy/schema checks or change the validator to accept drift. Run focused and complete solution validation, then hand actual logs to Testing/Validate/read-only Code Review. Completion: no untested HTTP/business behavior, contract drift, production security bypass or unrelated edits.

## Acceptance coverage

| Criterion | Steps and evidence |
|---|---|
| POST create `{}` → 201, write policy, correct GET Location | 3–5; real create, exact DTO, follow Location and fresh context |
| GET preview → 200, read policy, sanitized join preview | 3–5; host/member/nonmember views and no subject leakage |
| POST join `{}` → 200, write policy | 3–5; trusted second subject and Open duplicate idempotency |
| POST lock → 200, write plus host rule | 3–5; bodyless lock, repeat host lock and persisted Locked state |
| Missing 404; invalid code 400 | 1–5; every code route, auth-before-syntax and no route regex |
| Invalid capacity/state 409; nonhost lock 403 | 2–5; full/Locked joins, lone-host lock, nonhost Open/Locked ordering |
| Internal verify → 200, service-only exact verify policy | 3–5; valid Open/Locked provision plus browser/machine isolation |
| Safe ProblemDetails, thin controllers and public DTOs | 1–3, 5; five-key errors including binding/auth and six/five-key success |
| OpenAPI/security definitions agree; anonymous health works | 5–6; actual-route signed-token matrix, contract checks, health with absent/irrelevant token |

## Testing strategy

Use xUnit/FluentAssertions in the owned IntegrationTests project; no new packages or shared P09 infrastructure. Tokens are genuine RSA-signed JWTs accepted by the actual JwtBearer handler using the existing ephemeral OIDC metadata. This is not a fake authentication scheme or a claim that external Keycloak/live frontend acceptance has run.

- Lifecycle: create as host; follow relative Location with read scope; preview as host and unrelated user, checking exact keys and caller-relative flags; join as second subject, repeat while Open without a duplicate persisted member; third subject conflicts; host locks with no body, repeats successfully; existing and new members cannot join Locked. Read Locked state through a new SQL context and machine lookup. Nonhost lock is 403 for both Open and Locked; host with one member gets 409. Valid missing codes return 404 for preview/join/lock/verify, including nonhost lock of nonexistent lobby.
- Input: lower/mixed case, short/long and punctuation codes produce 400 on matching routes, not route 404. Valid code not in DB produces 404. Create/join `{}` succeed, while missing/null/malformed/scalar/array/unknown-property bodies fail safely. Cover hostId/playerId/userId and casing variants. Lock accepts bodyless and `{}` but supplied spoof/null/invalid objects do not bypass validation. Header/query attempts cannot choose host/joiner; verify persisted subjects using the machine endpoint/fresh SQL context. Unauthenticated malformed input returns 401 and wrong-scope malformed input 403 before 400.
- Actual-route security: for each of five routes, seed the necessary legitimate state and test missing, malformed, bad signature/unknown key, expired, wrong issuer/audience and ID-token-type failures as 401 with correct Bearer challenge; valid but missing/wrong/prefix/suffix scope, wrong client and wrong actor return 403. Browser with lobbies.verify is forbidden internally; machine with lobbies.read/write is forbidden publicly. A configured allowlisted machine with exact verify scope succeeds. Scope-array support remains covered by the existing matrix and gets representative actual-route confirmation. Do not let authorization failures reach a throwing repository just to prove denial; assert unchanged persisted state for denied mutations where applicable.
- Problems/failures: verify exact five-key shape, media type, matching status, bounded traceId and no leaks for syntax binding 400, auth 401/403, Domain 400/403/409, missing404, deterministic typed persistence/connectivity503 and unexpected500. Include malicious/sensitive sentinel exception/parser content and assert it never appears. Retain WWW-Authenticate for rejected tokens. A canceled request propagates cancellation and does not synthesize 500; coordinate any in-flight test with a bounded cancellation-aware service stub, not sleeps or a real unreachable dependency.
- Contracts: public results contain only lobbyId/joinCode/string status/memberCount/isMember/isHost, even for a scoped nonmember. Internal result contains exactly lobbyId/joinCode/string status/hostUserId/playerIds and permits Open one/two or Locked two members. Read committed OpenAPI and assert relevant operations/required input/security/shape/Location definitions agree; run the existing checker independently. Anonymous `/health` returns only `{status:"ok"}` with no token and with an invalid irrelevant bearer header, without SQL or peer dependency.

## Validation commands and environment

Run from the worktree root. .NET 10 and the existing LocalDB/QUIZARENA_TEST_SQLSERVER server convention are required for actual persistence tests; Node runs the dependency-free contract checker. Use unique disposable catalogs and never print connection credentials. Root runs outside-sandbox gates when necessary; workers immediately send the exact command/workdir/log request rather than waiting on their own escalation.

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror -m:1
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test tests/QuizArena.Lobby.IntegrationTests/QuizArena.Lobby.IntegrationTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.Lobby.UnitTests/QuizArena.Lobby.UnitTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj --no-build --verbosity normal
node contracts/validate-contracts.mjs
dotnet test QuizArena.sln --no-build --verbosity normal -m:1
git diff --check
```

Record actual command exits/counts/limits under `.dispatcher/runs/12-20261104/child-47/`; no prerequisite count is current evidence. Parent final validation retains coverage/frontend gates. Do not apply migrations to the development database as a test shortcut.

## Risks, blockers and exclusions

No #47 blocker is identified. Central problem customization must handle MVC/auth/exception/status paths consistently without stripping challenge headers or adding default `errors`/`instance` extensions. Optional lock binding must preserve the committed bodyless request and cannot open an identity input path. SQL/service unavailability must be classified narrowly, while cancellation and already-started responses remain intact. Dispose fixtures on failure and keep each test's catalog isolated.

Exclude accepted Domain/Data/application behavior changes, authentication policy redesign, fake bearer handlers, peer/legacy access, Gameplay/frontend, live external-service claims, shared fixture #24 and child #49's later vertical-slice audit. No commits, pushes, publication, issue closure or human signoff. Parent external #53/#24 constraints remain explicit for later stages; the final human gate belongs to the one parent draft PR.
