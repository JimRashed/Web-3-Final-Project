# Issue #46: Lobby contracts and application orchestration

## Summary and architecture

Issue [#46](https://github.com/JimRashed/Web-3-Final-Project/issues/46), S04-T03, joins parent [#12](https://github.com/JimRashed/Web-3-Final-Project/issues/12) in one final PR on `feature/m3/12-lobby-service`, based on `4f5d8cb`. This is an agent-reviewed plan; human review remains pending. Prime passed, #17 is complete and #44/#45 have passed all their stages on this branch.

Implement contracts and application services inside the owned `QuizArena.Lobby.Api`, using the accepted `QuizArena.Lobby.Data.Repositories.ILobbyRepository` and pure Domain decisions. Controllers, route/status/error mapping and bearer endpoint tests belong to #47. Keep accepted Domain/Data and legacy production code unchanged. The normative public/internal shapes come from `docs/milestone3/api-contracts.md` and `contracts/openapi/lobby.yaml`; internal lookup permits a valid Open one-member lobby as well as Locked two-member lobbies. Gameplay owns the requirement to verify Locked with two members before match creation.

## Exact file inventory

Create:

- `src/QuizArena.Lobby.Api/Contracts/EmptyLobbyRequest.cs`: empty create/join object with `JsonUnmappedMemberHandling.Disallow`.
- `src/QuizArena.Lobby.Api/Contracts/LobbyResponse.cs`: only lobbyId, joinCode, string status, memberCount, isMember and isHost.
- `src/QuizArena.Lobby.Api/Contracts/LobbyProvisionResponse.cs`: only lobbyId, joinCode, string status, hostUserId and copied read-only playerIds.
- `src/QuizArena.Lobby.Api/Services/ILobbyService.cs`: async application workflow interface taking a trusted `ClaimsPrincipal`, route code where applicable and cancellation token; no caller-chosen subject argument.
- `src/QuizArena.Lobby.Api/Services/LobbyService.cs`: actor checks, repository orchestration, pure lock callback and safe DTO mapping.
- `src/QuizArena.Lobby.Api/LobbyApplicationExtensions.cs`: scoped application service registration.
- `tests/QuizArena.Lobby.UnitTests/Services/LobbyServiceTests.cs`: strict mocked-repository application tests.
- `tests/QuizArena.Lobby.UnitTests/Services/LobbyContractTests.cs`: exact JSON/request shape and defensive-copy tests.
- `tests/QuizArena.Lobby.UnitTests/Services/LobbyRegistrationTests.cs`: scoped registration and constructor-based resolution with a substituted repository.

Modify:

- `src/QuizArena.Lobby.Api/Program.cs`: call the application registration extension; preserve existing persistence, security, middleware and anonymous health behavior.
- `tests/QuizArena.Lobby.UnitTests/QuizArena.Lobby.UnitTests.csproj`: replace the sole own Domain project reference with the sole own Api project reference; existing Domain tests use transitive references. Add no package or extra project reference.
- `tests/QuizArena.ArchitectureTests/ServiceBoundaryTests.cs`: minimally allow exactly one own Domain **or** own Api reference for a UnitTests project; retain exactly one own Api reference for IntegrationTests. Add direct positive/negative tests of the test-reference rule without mutating on-disk projects. Keep all production dependency rules, Domain purity checks and frontend checks intact.

No deletions, Data/Domain changes, peer references or Gameplay client. A cohesive filename adjustment must be recorded in this scoped plan rather than silently expanding scope.

## Ordered steps

1. **Enable application unit coverage without relaxing production boundaries.** Change the owned unit project's single reference to own Api. Factor the existing test-project reference predicate only as needed to test it against in-memory XML. Unit tests may reference exactly their own Domain or exactly their own Api, never both, own Data alone, peers or legacy; integration tests remain own Api only. Preserve every production project rule. Completion: existing Domain tests still build and architecture positives/negatives verify the narrowly extended rule.
2. **Define exact contracts.** Use DTO records/classes in Api, never EF or Domain entities as response/input types. Make status an explicit `Open`/`Locked` string. EmptyLobbyRequest has no identity or other properties and rejects unmapped JSON members using its type-level System.Text.Json attribute. Under normal web JSON options, `{}` succeeds; hostId/playerId, differently cased variants and arbitrary extra properties throw `JsonException`. Array/scalar bodies fail type deserialization; HTTP empty/null/malformed-body behavior is tested in #47. Use explicit serialized property names or the application's normal web naming options consistently. Provision copies member subjects into a read-only snapshot rather than exposing a live Domain collection. Completion: serialized public JSON contains exactly six approved keys and no host/subject/player/version/navigation data; provision contains exactly the five approved keys.
3. **Define the application interface and actor boundary.** Provide `CreateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)`, `PreviewAsync(ClaimsPrincipal principal, string joinCode, CancellationToken cancellationToken = default)`, `JoinAsync(...)`, `LockAsync(...)` and `VerifyAsync(...)`, with the latter four taking the same principal/code/token parameters. Create returns LobbyResponse; preview/join/lock return nullable LobbyResponse; verify returns nullable LobbyProvisionResponse. Before any repository access, require `principal?.Identity?.IsAuthenticated == true` and extract the existing exact actor via `GetAuthenticatedActor`. Do not treat the presence of claims as proof of authentication. Missing/invalid actor metadata or unauthenticated identity throws a fixed-message `UnauthorizedAccessException`; preserve the extraction failure as an inner exception if useful. Public workflows require ActorKind.User; verification requires ActorKind.Machine, with wrong kind rejected via `LobbyForbiddenException`. Scope, audience and token validation remain existing security/#47 policy responsibilities. Do not change the accepted shared security helper. Completion: anonymous, claim-only unauthenticated, malformed and wrong-kind principals make zero repository calls; exact subject comes solely from the principal.
4. **Orchestrate accepted persistence and Domain operations.** Validate supplied codes with existing `JoinCodeGenerator.Validate` after actor checks; never uppercase them. Create forwards the trusted subject to `CreateAsync`; preview uses `GetByJoinCodeAsync`; join forwards the trusted subject to repository `JoinAsync`. Lock calls repository `SaveAsync(joinCode, lobby => lobby.Lock(subject), token)`. The lock callback captures only the immutable trusted subject and is synchronous/pure, so retries can rerun Domain authorization and transition decisions against each reloaded aggregate. Do not read-and-decide once then save a stale aggregate. For missing repository results return null for #47's future 404 mapping. Map the actual returned aggregate after persistence, rather than a speculative request state. Forward the exact CancellationToken; check pre-cancellation before starting repository work. Let Domain, persistence, repository and cancellation exceptions propagate without wrapping them as success, not-found or HTTP responses. Completion: tests verify calls, tokens, null handling, failures and repeated execution of the lock callback on independent aggregate instances.
5. **Map safe caller-relative/public and machine-only/internal results.** Compute isHost and isMember with exact ordinal subject comparisons; preserve opaque case and do not infer membership merely from being authenticated. Public preview for a scoped nonmember is valid with false flags and no subject leakage. All public workflows use the same mapper. Verification returns the actual stable lobby ID/code/state, host subject and copied member subject list for any valid persisted aggregate: Open with one/two members or Locked with two. Do not require Locked in Lobby verification, create a match or call Gameplay. Completion: host/member/outsider and case-distinct subjects receive correct flags, and changing the source aggregate later cannot change a previously returned provision DTO.
6. **Register and verify.** Add scoped ILobbyService/LobbyService registration and one Program call. Use constructor injection of the existing repository only; no HttpContext accessor, static service locator, secrets or database I/O in application unit tests. Add the tests below, run checks and hand actual evidence to Testing/Validate/read-only Code Review. Completion: scoped resolution works with a substitute repository; existing persistence/security/health behavior remains unchanged.

## Acceptance coverage

| Criterion | Steps and evidence |
|---|---|
| Api DTOs/interfaces are owned by Api; no HTTP DTO dependency in Domain | 2–3; exact contract tests and unchanged Domain project/source |
| Host/joiner derived from trusted principal, never client IDs | 2–4; empty DTO rejection, zero-I/O actor rejection and strict repository argument verification |
| Create/get/join/lock call Domain invariants through repositories | 4; successful/missing/failure cases, Domain transitions and repeatable Lock callback |
| Safe public preview and ordinal caller-relative flags | 2, 5; exact JSON plus host/member/outsider/case tests |
| Internal host/subjects/state lookup for Gameplay | 3, 5; machine-only boundary, Open/Locked data and independent copied collection |
| No match creation, Gameplay tables or cross-service references | 1–6; no client/peer dependency and architecture checks |
| Cancellation and dependency failures | 4; exact token forwarding, canceled/faulted tasks and zero work on pre-cancellation |
| Mocked application unit and architecture tests | 1, 6; own Api reference with negative architecture coverage, all existing Domain tests retained |

## Testing strategy

Use xUnit/FluentAssertions and the already installed Moq in the owned UnitTests project. Create authenticated ClaimsIdentity instances with existing QuizArena claim names and a real authentication type for application tests; distinguish unauthenticated identities with identical claims. These are in-memory application inputs, not substituted bearer handlers. Real signed-token endpoint coverage belongs to #47.

- Strict mock repository tests: create host argument; preview host/member/nonmember flags; trusted joiner argument; nullable missing preview/join/lock/verify; successful host lock and idempotent repeat; pure Save callback executed more than once on independently reconstructed aggregate instances. Execute the callback to prove nonhost forbidden before state and one-member host conflict; do not merely assert that some delegate was supplied.
- Actor guard tests across public workflows: null/anonymous principal, unauthenticated identity carrying valid-looking claims, missing issuer/subject/actor, unrecognized actor kind and machine actor rejection without any repository calls. Verification rejects user/unauthenticated principals; valid machine receives provision. Preserve exact case-distinct subject values and reject invalid codes before persistence, without normalization.
- Propagation tests: each workflow forwards its supplied token; pre-canceled requests never call persistence; repository cancellation is preserved. Representative typed Domain failures, LobbyPersistenceException and generic dependency failures pass through unchanged, with no response fabricated. Cover Open duplicate/full/Locked joins through the mock executing the real Domain operation as appropriate rather than duplicating Domain rule implementations in the service.
- Contract tests using `JsonSerializerDefaults.Web`: exact public six-key and provision five-key sets, correct camelCase names and string statuses, forbidden subject/rowversion/navigation fields absent, `{}` accepted and unknown identity/arbitrary members rejected. Provision supports Open one/two and Locked two members. Capture provision then mutate the source Open aggregate with a second member; the earlier list stays unchanged and cannot be mutated through the exposed collection.
- Registration tests: resolve ILobbyService with a mock repository in an ordinary service provider; same scoped service within one scope and distinct services across scopes. No server or SQL is required for focused application tests.
- Architecture tests: sole own Domain and sole own Api unit references pass; zero, multiple, own Data, peer Domain/Api and legacy references fail. Integration own Api passes while own Domain/Data and peer/multiple fail. Existing production Domain/Data/Api, package purity and frontend boundary assertions remain unchanged.

## Validation commands and environment

Run from the worktree root with .NET 10. Focused application tests need no external service; full regression also runs accepted SQL Server persistence tests using the existing LocalDB/QUIZARENA_TEST_SQLSERVER convention. Do not expose connection secrets. Workers send exact commands/log paths to root for outside-sandbox execution if needed; they must not stall on their own escalation prompts.

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror -m:1
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test tests/QuizArena.Lobby.UnitTests/QuizArena.Lobby.UnitTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj --no-build --verbosity normal
dotnet test QuizArena.sln --no-build --verbosity normal -m:1
git diff --check
```

Record actual exits, counts and limitations under `.dispatcher/runs/12-20261104/child-46/`. No historical child test count is current evidence. Parent final validation retains coverage and frontend gates; no frontend changes are required here.

## Risks, blockers and exclusions

No #46 implementation blocker is identified. The current principal extractor does not itself enforce authenticated identity; the application guard must do so before repository calls. The scoped architecture change is necessary to unit-test Api application orchestration and must preserve the sole-own-reference rule and all production boundaries. Typed exceptions are application outcomes for #47 to map; this child must not add controllers, HTTP responses, ProblemDetails handling or bearer bypasses to make its unit tests pass.

Exclude accepted Domain/Data/legacy changes, public identity inputs, EF response serialization, match creation, Gameplay clients/tables, network dependencies, frontend, security policy redesign and shared fixture #24. External #53/#24 remain parent constraints on later children; they do not block these application workflows. No commits, pushes, PR publication, issue closure or fabricated human approval. Final human signoff stays pending on the single parent PR.
