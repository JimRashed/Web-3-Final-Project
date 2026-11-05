# Issue #49: Lobby coverage audit and missing acceptance tests

## Summary and architecture

Issue [#49 — S04-T06](https://github.com/JimRashed/Web-3-Final-Project/issues/49), parent [#12](https://github.com/JimRashed/Web-3-Final-Project/issues/12), epic [#7](https://github.com/JimRashed/Web-3-Final-Project/issues/7). Prime verified open assignment to JimRashed, P0, no duplicate work, and closed hard prerequisites #47/#48/#24. Worktree `issue-49-lobby-coverage`, branch `feature/m3/49-lobby-coverage`, milestone base `3a9b9d26330259dd15804ea2f37b2efdd0995bac`.

Audit the tests already delivered with the Lobby layers, add genuine uncovered acceptance scenarios, and publish reproducible validation evidence. Preserve all production code, contracts, security policies, package/project dependencies and unrelated tests. This is an agent-reviewed plan under the current dispatcher policy, using `specs/issue-49-plan.md`; old intermediate human gates and plan paths are superseded. Independent human approval remains pending at the final draft PR.

The service boundary is the existing Lobby API/Domain/Data. Existing HTTP fixtures retain real JwtBearer validation with ephemeral RSA tokens and isolated SQL Server LocalDB, and verify the factory points at its disposable catalog. No fake authentication handler, test production bypass, shared real database or service-reference expansion is needed. UI verification uses existing Session/HTTP clients with controlled fetch doubles. A two-session component workflow is useful evidence, but is never a live provider/backend/Gameplay acceptance claim.

## Existing coverage inventory and genuine gaps

| Acceptance area | Existing evidence to execute and audit | Planned addition only where absent |
|---|---|---|
| Create/join/duplicate/full/locked/host transitions | `Domain/ActiveLobbyTests.cs`: create, second member, duplicate idempotence, full/locked failures, host/nonhost lock and no mutation; `Services/LobbyServiceTests.cs`: trusted subjects, actor checks, cancellation and repository outcomes | Reuse existing tests; do not duplicate equivalent happy/negative paths merely to increase counts |
| Relational uniqueness/capacity | `Persistence/LobbyConstraintTests.cs`: duplicate code/member, FK/cascade and rollback; `LobbyConcurrencyTests.cs`: last-slot races, duplicate subject convergence, join/lock overlap, bounded retries and cancellation | Inspect fresh-context assertions and isolation; extend only if a required outcome is currently only tracked/in-memory |
| Real HTTP lifecycle/statuses | `Endpoints/LobbyLifecycleTests.cs`: create/get/join/repeated join/third player409/host lock/locked join/internal lookup and fresh-context lock+roster; `LobbyInputTests.cs`, `LobbyErrorTests.cs`, `LobbyContractTests.cs` cover syntax, safe failures and DTO ownership | Add claim/subject/service isolation against actual routes, rather than policy probe endpoints alone |
| Positive/negative token/scope matrix | `Endpoints/LobbyRouteSecurityTests.cs`: all five routes across missing/malformed/signature/key/expiry/issuer/audience/type/scope/client/actor failures, positive scp-array grants; `Security/SecurityMatrixTests.cs`: shared policy matrix/CORS | Audit missing/invalid subject, exact subject preservation/case distinctions and explicit trusted-service restriction on actual endpoints; add uncovered cases with persisted no-mutation checks |
| UI two-player lifecycle | Existing `LobbyPage.test.tsx` covers separate host/guest views, lock/start200/201, status errors/nonhost disabled; lifecycle file covers polling, stale updates, expiry/session replacement and duplicate submissions | Add one coherent two-session host-create/guest-join/host-refresh/lock/start scenario against a shared controlled public-response state; prove guest controls remain disabled before/after lock and only host can issue Gameplay start |
| Evidence and live acceptance | No current aggregate `docs/milestone3/validation-results.md` | Create dated #49 section with test mapping, actual commands/counts, isolation/security findings and explicit live/human limitations |

Before adding a test, confirm it would fail for a meaningful regression not already detected by an equivalent current case. No production change is authorized by this plan: discovered product failures return to Dispatcher for scoped assessment rather than silently weakening assertions.

## Exact file inventory

| Action | Path | Purpose |
|---|---|---|
| Create | `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyPrincipalIsolationTests.cs` | Actual-route missing/malformed subject and trusted actor/client isolation, spoof-resistant exact identity outcomes |
| Create | `src/QuizArena.Client/src/features/lobby/LobbyPage.multiplayer.test.tsx` | Coherent independent host/guest component workflow with existing doubles and no new library |
| Create | `docs/milestone3/validation-results.md` | #49 acceptance audit, actual validation and pending live/human evidence |
| Create | `specs/issue-49-plan.md` | This plan |

Existing unit, repository, HTTP security/lifecycle and frontend files are read/execute audit inputs. Modify an existing test file only for a proven missing assertion or shared-fixture necessity; record exact path/reason in this inventory before editing. Prefer current `LobbyApiFixture`, `TestTokens`, `LobbyDatabase`, `sessionDouble`, `deferred` and Testing Library helpers without changing them. Do not add dispatcher-specific test folders, new projects, dependencies or E2E framework. No deletions are planned.

## Ordered steps

1. **Baseline and audit.** Read actual #49 and normative Lobby API/authorization contracts; map every criterion to named existing tests. Run baseline Lobby unit/integration and current Lobby frontend files before edits. Inspect fresh-context assertions, fixture ownership/unique database names and real bearer configuration. Record failures separately and do not claim historical counts as fresh results. Parent reports `npm ci` completed against the committed lockfile; preserve its actual output rather than concurrently repeating installation.
2. **Fill actual-route principal isolation gaps.** Use `LobbyApiFixture` and signed `TestTokens` to exercise missing/blank/oversized subject as applicable to current policy, with exact expected401/403 from normative authentication/actor rules rather than guessed status. Verify no create/join/lock write occurred through a new DbContext. Cover authenticated subject identity remaining ordinal/case-sensitive and unmodified despite spoofed query/header/body inputs where those requests are contract-valid; forbidden nonhost lock must stay403 and preserve persisted state. Reuse existing lifecycle/spoof tests when they already establish an outcome. For internal lookup verify only the configured Gameplay service actor/client can obtain private roster data, including another service client rejected even with matching scope/audience and valid signature; browser/user and anonymous/invalid token outcomes remain covered. Do not impose self/participant ownership on public previews: safe nonmember preview is intentionally allowed; host-only lock and machine-only roster are the applicable isolation rules.
3. **Fill the coherent UI workflow gap.** Mount host and guest with separate real Session objects and routers, query each render using scoped `within` to avoid ambiguous controls. Keep one controlled fetch double representing approved public Lobby responses and the separate Gameplay response; use each session's distinct test token to derive the caller-relative flags in the double. Host creates, guest enters the returned code and joins, host observes two players via controlled normal refresh, host locks, both views observe Locked, guest lock/start remain disabled, and host starts exactly once with `{joinCode}` only and navigates to the actual returned match ID. Verify no premature Gameplay call, no internal route, no caller-supplied host/user identity and no guest mutation through disabled controls. Keep fake timer advancement bounded and flush promises deterministically; restore timers/stubs and unmount both views to remove polls. This double intentionally models public service behavior and does not prove its server implementation or live Gameplay availability. Existing HTTP/SQL tests supply the server evidence independently.
4. **Record the acceptance audit.** In `docs/milestone3/validation-results.md`, add the issue/parent/base, exact test-to-criterion table, changed path ownership and security constraints, baseline/new test results, commands/exits/counts/warnings, coverage interpretation, isolation/fresh-context checks and failures/remediation. Initially label not-yet-run final gates pending; update from real Validate evidence only. Link the scoped plan, eventual draft PR and read-only review when known. Keep raw TRX/coverage/logs and operational scripts ignored under `.dispatcher/runs/49-20261104/`; no credentials/tokens/connection strings in tracked evidence.
5. **Testing, full validation and read-only review.** Testing independently inspects/adds meaningful missing cases; Validate executes the required fresh gates and separate coverage run; review sees the full diff and actual evidence. Workers do not commit/push/approve/merge/close. Dispatcher owns a conventional commit, draft milestone PR, owner assignment and independent reviewer request after applicable code gates pass. Clearly retain pending live/human acceptance instead of describing a draft as Done.

## Acceptance mapping

| #49 requirement | Steps / evidence |
|---|---|
| Unit create/join/duplicate/full/locked/host | 1,5; named current Domain/Services tests, freshly executed |
| Relational code/membership uniqueness and concurrent capacity | 1,5; existing constraint/concurrency tests with fresh contexts and unique owned LocalDB catalogs |
| HTTP create/get/join/lock/internal lookup, spoofed host403/404/409, persisted lock | 1–2,5; current lifecycle/input/error tests plus missing actual-principal cases |
| UI two-player create/join/lock/start, errors/nonhost disabled | 1,3,5; new coherent workflow plus existing status/lifecycle tests |
| Full token/scope and applicable identity/service isolation matrix | 1–2,5; existing positive/negative matrix plus real-route principal isolation assertions |
| Persisted outcomes, isolation and live frontend/backend acceptance | 1–2,4–5; SQL contexts/fixture audit; live checklist below stays pending when unavailable |
| Changed path ownership/layer/security verification | Inventory,4–5; test-only production boundary and read-only review |
| Frontend gates, Lobby tests, solution/architecture regression, evidence | 1,4–5; exact commands and actual results |
| Plan/validation/review/independent human approval | This file, validation document and final draft PR; human review remains pending |

## Test standards and validation commands

C# tests follow existing xUnit/FluentAssertions/Moq conventions, descriptive `Method_Scenario_ExpectedBehavior` names and Arrange/Act/Assert. Unit tests remain free of I/O. SQL concurrency tests use existing deterministic interceptors/barriers, never arbitrary sleeps. Use unique generated SQL catalogs and clean up only fixture-owned databases. Fresh contexts with AsNoTracking verify stored results rather than trusting an already tracked aggregate. Frontend tests use Vitest/Testing Library with distinct Session doubles and deterministic timers; do not add product-like business logic beyond the minimal protocol response double.

From repository root (.NET10, provisioned LocalDB/SQL Server, cached packages):

```powershell
dotnet restore QuizArena.sln --source C:\Users\kilda\.nuget\packages --disable-parallel -m:1 --verbosity minimal
dotnet build QuizArena.sln --no-restore -warnaserror -m:1 --verbosity minimal
dotnet test tests/QuizArena.Lobby.UnitTests/QuizArena.Lobby.UnitTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.Lobby.IntegrationTests/QuizArena.Lobby.IntegrationTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj --no-build --verbosity normal
dotnet format QuizArena.sln --verify-no-changes --no-restore --verbosity minimal
dotnet test QuizArena.sln --no-build -m:1 --verbosity normal --logger 'trx;LogFilePrefix=regression' --results-directory .dispatcher/runs/49-20261104/regression-results
dotnet test QuizArena.sln --no-build -m:1 --collect:'XPlat Code Coverage' --logger 'trx;LogFilePrefix=coverage' --results-directory .dispatcher/runs/49-20261104/coverage-results
node contracts/validate-contracts.mjs
git diff --check
```

The architecture project path above is verified in this base. A cache miss or environment failure is reported, never bypassed. Save real discovered/passed/failed/skipped totals and distinguish test assertions from unexecuted fault branches.

From `src/QuizArena.Client`:

```powershell
npm ci
npm run typecheck
npm run lint
npm run test -- --run src/api/lobby.test.ts src/features/lobby/LobbyPage.test.tsx src/features/lobby/LobbyPage.lifecycle.test.tsx src/features/lobby/LobbyPage.multiplayer.test.tsx src/app/App.test.tsx
npm run test -- --run
npm run build
```

`npm ci` was already executed by Dispatcher against this worktree; use the existing successful run as current install evidence and preserve lockfile. Record current advisories without upgrading unrelated dependencies. `npm run test:e2e` is absent in the current package; record unavailable rather than manufacturing a passing suite or silently replacing it with component tests. Sandbox-specific process/cache failures should be reported for authorized rerun; do not label them product passes.

## Live acceptance, risks and exclusions

Closed #47/#48/#24 permits this audit, but does not prove live services exist. Prime reports external Gameplay [#53](https://github.com/JimRashed/Web-3-Final-Project/issues/53) still open with draft PR#96; this blocks a claim of real two-player start completion. Dispatcher freshly probed provider discovery8080, Lobby health5104 and Gameplay health5105: each timed out after four seconds; Docker is unavailable. Actual probe results are saved locally in `live-environment.json`. Do not repeat unchanged probes or assume they prove a permanent condition; retry live validation if the environment changes. A running provider can exist without Docker installed. Do not print tokens or collect credentials in chat.

When provider, Lobby, real Gameplay and authorized test accounts are available, exercise two independent browser sessions: host create, guest join shared code, host sees two members, guest lock/start disabled, host lock persists, host start returns/navigates to the actual match, and safe error/session teardown behavior. Participant entry/event delivery belongs to the peer flows and must not be inferred from a link. If those capabilities are absent, record exact probes/dependency status and retain live acceptance pending. No test double, local signed JWT fixture or passing build substitutes for provider-backed browser acceptance. Do not close #49 or invent human Gate2/Gate3 evidence.

No production fixes, authorization weakening, fake login/endpoints, service machine secrets, new packages, changed contracts, Gameplay implementation, broad E2E infrastructure, skipped/deleted tests, historical evidence rewriting or unrelated feature work. If a new test exposes a product defect, stop that change and return exact evidence to Dispatcher. Default repair budget remains two additional cycles after the initial run; all post-edit validation/review must be fresh.

## Publication base documentation integration

After runtime validation, the milestone advanced to `211b84828419296ba4c12b57d6093a09135749e9` with documentation-only report #110. The issue branch fast-forwarded to preserve that report and appended the #49 audit to its validation-results document. Production and test files remained byte-for-byte unchanged; only evidence prose and this plan note changed. Static verification follows before read-only review. Runtime validation totals refer to the exact original base plus #49 tests.
