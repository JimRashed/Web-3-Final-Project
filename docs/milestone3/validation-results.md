# Milestone 3 validation results

These are actual results, recorded on 2026-11-05 against `main` at `0b5580a` (the merge of #123, which carries the whole milestone branch). The CI workflow `.github/workflows/ci.yml` runs the same backend, architecture, security and frontend gates on every PR.

## Build and static checks
| Command | Result |
|---|---|
| `dotnet build QuizArena.sln -warnaserror` | 0 warnings, 0 errors |
| `dotnet format QuizArena.sln --verify-no-changes` | clean |
| `npm run typecheck` / `npm run lint` (client) | clean, 0 warnings |
| `npm run build` (client) | succeeds |

## Automated tests (`dotnet test QuizArena.sln`, `npx vitest run`)
| Assembly | Passed |
|---|---|
| QuizArena.ArchitectureTests | 21 |
| Identity unit / integration | 39 / 135 |
| Statistics unit / integration | 49 / 207 |
| QuestionBank unit / integration | 70 / 162 |
| Lobby unit / integration | 174 / 285 |
| Gameplay unit / integration | 65 / 97 |
| Notification unit / integration | 29 / 105 |
| **Backend total** | **1,438 passed, 0 failed, 0 skipped (13 assemblies)** |
| React client (25 files) | **305 passed** |

The legacy aggregate-host test projects were removed with the host in D02 (#115).

The relational tests run against a real SQL Server (LocalDB locally, a SQL Server 2022 container in CI), and each test class gets its own uniquely named database.

## Security evidence
- **Provider (P05).** The browser client is refused the password grant, client credentials, the implicit flow, missing or `plain` PKCE, machine scopes and unregistered redirects. A real PKCE S256 sign-in yields the expected audiences and scopes. See [authentication](authentication.md#verification-evidence).
- **APIs (P06, P09).** Every service runs a security matrix with real signed JWTs. For Identity and Statistics the full matrix also runs against the real routes (#104):

  | Cases | Expected | Result |
  |---|---|---|
  | missing, malformed, bad signature, unknown key, expired, no expiry, wrong issuer, wrong audience, ID token | 401 with an `invalid_token` challenge | passing |
  | missing or prefix/suffix scope, no subject, wrong `azp`, wrong or missing `qa_actor`, other actor's token | 403 | passing |

  Rejected writes persist nothing.

## Live checks (Keycloak + APIs + Vite + Edge via Playwright)
| Slice | Checks | Evidence |
|---|---|---|
| Identity + Statistics: sign-in, save the profile, Gameplay machine client records results, statistics and leaderboard render | 9/9 | PR #102 |
| QuestionBank: previews with no `correctAnswer` anywhere, count validation, 200/400/401, player token refused on the internal route (403) | 9/9 | PR #106 review |
| **D03 browser journey** (`tests/e2e`, `npm run journey:ui`): two players through the React screens, from lobby to the end screens, with the guest opening the match from their delivered event, the result in both players' Statistics and no 5xx | **11/11** on `main` | PR #121 |
| **D03 API journey** (`npm run journey:api`): the same game with simultaneous answers, ownership checks, and outbox delivery to Statistics and Notification | **10/10** on `main` | PR #118 |
| **D03 delivery retry** (`npm run journey:retry`): Statistics down when the match finishes; results stay Pending with retries; delivered after the restart | **8/8** on `main` | PR #121 |
| D02 cutover rehearsal (`scripts/migrate-legacy-data/Invoke-Rehearsal.ps1`) | 34/34 | PR #115 |

## Defects found by live testing and review (all merged with fixes)
| Area | Defect | Fix |
|---|---|---|
| Gameplay #95/#96/#100 | a repeated start returned the match to non-hosts; simultaneous starts and answers returned 500; the match page was unreachable and showed the guest the host's score | #107, #108, #109 |
| Gameplay outbox #111 | the machine-token options bound the wrong config keys, and the OAuth response was read as camelCase, so **every match start returned 503** | #118 |
| Client | the guest had no in-app way to reach the match (`/notifications` placeholder, no link) | #121 |
| Gameplay outbox (#111 review, P3) | completion writes didn't check lease ownership, so a worker that outlived its lease could overwrite the new owner's result | this PR (lease-fenced writes and the first SQL tests of the claim) |

## Still open
| Item | Status |
|---|---|
| Notification `GET /events` | merged without the contract's paging and `userId` 403 rule (review on #90). Follow-up needed. |
| D01 #62 | a teammate's clean-checkout run of the runbook is still to be recorded |
| D06 #67 | final read-only review and signoff |

---

## Issue #49 — Lobby coverage audit

Date: 2026-11-04. Issue [#49](https://github.com/JimRashed/Web-3-Final-Project/issues/49), parent [#12](https://github.com/JimRashed/Web-3-Final-Project/issues/12), milestone base 3a9b9d26330259dd15804ea2f37b2efdd0995bac. The [scoped plan](../../specs/issue-49-plan.md) is agent-reviewed. Final automated validation passed as recorded below. Read-only review and independent human approval are pending and will be recorded on the draft PR; live acceptance remains pending.

### Acceptance coverage

| Requirement | Existing executed coverage | New coverage |
|---|---|---|
| Create, join, duplicate/full/locked transitions and host rules | Domain/ActiveLobbyTests.cs; Services/LobbyServiceTests.cs; baseline Lobby unit suite 174 passed | Existing assertions retained |
| Relational uniqueness, capacity and concurrency | Persistence/LobbyConstraintTests.cs and LobbyConcurrencyTests.cs; baseline Lobby integration suite 277 passed | Existing SQL fixture and ownership retained |
| Actual HTTP lifecycle, input/status and persisted lock | Endpoints/LobbyLifecycleTests.cs, LobbyInputTests.cs, LobbyErrorTests.cs and LobbyContractTests.cs; baseline integration suite passed | LobbyPrincipalIsolationTests.cs verifies fresh-context no mutation for rejected actor cases |
| Signed token, scope, client, actor and caller identity | Endpoints/LobbyRouteSecurityTests.cs and Security/SecurityMatrixTests.cs; baseline integration suite passed | Eight actual-route principal cases cover absent/blank subject, oversized join subject, case-sensitive preview/lock and wrong service/browser access to private roster |
| Two-player UI create/join/refresh/lock/start | Existing LobbyPage.test.tsx and LobbyPage.lifecycle.test.tsx; baseline focused frontend run 106 passed | LobbyPage.multiplayer.test.tsx verifies one coherent two-session public workflow and host-only Gameplay call; 1 passed |
| Real provider, Lobby and Gameplay browser flow | Not established by automated doubles or local signed-token fixtures | Pending live checklist below |

The eight new principal cases use the real JwtBearer validation pipeline, signed ephemeral RSA test tokens and an isolated fixture-owned SQL catalog. Rejected create/join/lock routes are checked through new DbContext instances with AsNoTracking rather than an already tracked entity. The fixture verifies that its repository connection targets its own disposable catalog. Public preview remains available to scoped nonmembers, with caller-relative flags and no private roster. The internal roster route rejects a validly signed token from another service client even when audience and scope match, and rejects a browser token. These tests do not simulate provider claim issuance.

The UI test mounts separate authenticated Session objects and routers. A controlled fetch double returns only public Lobby responses and the separate Gameplay match response. It proves the guest controls stay disabled, the host observes a normal refresh before lock, and the browser sends exactly one host Gameplay start with only the returned join code. It does not prove server behavior or live provider-backed navigation.

### Actual implementation-stage checks

| Command | Result |
|---|---|
| dotnet restore QuizArena.sln from cached NuGet source, serial | Exit 0 |
| dotnet build QuizArena.sln --no-restore -warnaserror -m:1 | Exit 0, zero warnings/errors before test edits |
| dotnet test tests/QuizArena.Lobby.UnitTests/QuizArena.Lobby.UnitTests.csproj --no-build | Exit 0, 174 passed, 0 failed |
| dotnet test tests/QuizArena.Lobby.IntegrationTests/QuizArena.Lobby.IntegrationTests.csproj --no-build | Exit 0, 277 passed, 0 failed, 0 skipped before test edits |
| npm run test -- --run Lobby API/page/lifecycle/App files | Exit 0, 4 files and 106 tests passed before test edits |
| dotnet build tests/QuizArena.Lobby.IntegrationTests/QuizArena.Lobby.IntegrationTests.csproj --no-restore -warnaserror -m:1 | Exit 0, zero warnings/errors after new C# test |
| dotnet test Lobby integration project --no-build --filter FullyQualifiedName~LobbyPrincipalIsolationTests | Exit 0, 8 passed, 0 failed/skipped |
| npm run test -- --run src/features/lobby/LobbyPage.multiplayer.test.tsx | Exit 0, 1 passed after correcting a test-only scoped query |
| npm run typecheck | Exit 0 after test edits |
| npm run lint | Exit 0, zero warnings after test edits |

The first new UI test run failed because both simulated browser tabs shared one jsdom document, making duplicate field IDs ambiguous to a name query. The guest textbox query was scoped to its render, and the rerun passed. No product code changed. The independent Validate stage subsequently completed the aggregate tests, format, contract, coverage and frontend gates; exact counts and exits are recorded below.

### Scope and pending acceptance

Changed tracked scope is this audit and the scoped plan; the only test additions are Endpoints/LobbyPrincipalIsolationTests.cs and LobbyPage.multiplayer.test.tsx. No production, authorization, contract, dependency, lockfile or existing test file was changed. Raw logs, TRX and coverage files remain ignored under .dispatcher/runs/49-20261104/.

Dispatcher's current environment probe timed out after four seconds for provider discovery on port 8080, Lobby health on 5104 and Gameplay health on 5105; Docker was unavailable on PATH. Gameplay [#53](https://github.com/JimRashed/Web-3-Final-Project/issues/53) remains open. No live two-player browser flow, CI link, PR URL, read-only review verdict or human signoff is claimed here.

When the environment is available, use two normal user browser sessions to verify host create, guest join, host refresh, guest-disabled controls, persistent host lock and actual Gameplay match navigation. Exercise safe errors and session teardown without bypassing bearer authorization. Record real observations and independent PR approval before closing #49.

### Final independent validation

The following gates ran on milestone commit `3a9b9d26330259dd15804ea2f37b2efdd0995bac` plus the #49 tests. Publication integrates documentation-only milestone commit `211b84828419296ba4c12b57d6093a09135749e9`; no runtime or test file changed during that integration. The earlier milestone report above is preserved as historical evidence and is not a claim that this run exercised a live provider.

| Gate | Actual result |
|---|---|
| Offline solution restore | Exit 0 |
| Solution build with warnings as errors | Exit 0; 0 warnings, 0 errors |
| Format verification | Exit 0 |
| Full backend regression | Exit 0; 1,356 passed, 0 failed/skipped; 15 test assemblies |
| Separate XPlat coverage test run | Exit 0; 1,356 passed, 0 failed/skipped; 15 coverage artifacts |
| Focused architecture suite | Exit 0; 21 passed |
| Contract validator | Exit 0; 30 operations, 179 examples, 12 corruption probes |
| Frontend typecheck, lint and production build | Each exit 0; lint 0 warnings |
| Full frontend suite | Exit 0; 289 passed across 23 files |
| Independent focused Lobby tests | 174 unit, 285 integration, 107 frontend passed; 0 failed/skipped |
| Locked frontend dependency install | Exit 0; 254 packages installed, lockfile unchanged |

Commands from the solution root: `dotnet restore QuizArena.sln --source C:\Users\kilda\.nuget\packages --disable-parallel -m:1`, `dotnet build QuizArena.sln --no-restore -warnaserror -m:1`, `dotnet format QuizArena.sln --verify-no-changes --no-restore`, `dotnet test QuizArena.sln --no-build -m:1 --verbosity normal`, a separate `dotnet test QuizArena.sln --no-build -m:1 --collect:"XPlat Code Coverage"`, focused Lobby/architecture project tests, and `node contracts/validate-contracts.mjs`. From the client directory: `npm ci`, `npm run typecheck`, `npm run lint`, `npm run test -- --run`, `npm run build`. The package has no `test:e2e` script; no E2E suite pass is claimed. Coverage collection does not establish an unmeasured percentage or branch guarantee.

The final draft PR will link the automated stages, with independent human signoff and the live checklist still pending. No manual issue closure or Done status is justified by these results alone.

---

## Issue #43 — QuestionBank coverage audit

This section records the #43 audit on `feature/m3/43-questionbank-coverage`, initially based on milestone commit `a79f9ba90cc1a114019db93200289be3cf6cbeb1` and refreshed to `a5f973f3caa2c79a203db735d98d3d78ff33dbb9` before all final gates. Earlier sections remain historical evidence, including the PR #106 live observations; they are not new live results from this run. Scoped plan: [issue-43-plan.md](../../specs/issue-43-plan.md). Parent feature #11; epic #7.

### Acceptance coverage

| Requirement | Executed existing or added evidence |
| --- | --- |
| Option/answer invariants, boundaries and immutability | Existing `QuestionTests`, `QuestionSelectionTests`, `QuestionCatalogServiceTests` and unit contract tests retained. These cover four distinct options, answer membership, count1/50, invalid counts, duplicate selection, copying and public mapping. |
| Real HTTP + SQL vertical slice | New `QuestionBankSqlApiFixture` retains and verifies production `QuestionRepository` and `QuestionCatalogService`, with real JwtBearer validation and a unique migrated SQL database per case. Only context configuration is replaced. |
| Public safe projection and selection | New `QuestionSqlEndpointsTests` requests count1/default10/count50, checks unique IDs and exact four-field answer-free JSON, compares prompt/category/ordered options by ID against fresh persisted records and verifies complete catalog immutability. |
| Internal answer material and shortage | New SQL tests compare five distinct machine snapshots and answers to persisted values. Owned four-question databases demonstrate public available-count behavior and safe internal409 without catalog mutation. Snapshot responses are not persisted match records. |
| Authentication, scopes and principal separation | Existing actual-route `QuestionRouteSecurityTests` and `SecurityMatrixTests` cover signature, issuer, audience, expiry, token type, scopes and actor/client requirements. New SQL-backed cases cover no-expiry401, empty/whitespace subject403, browser/internal and machine/public denial, wrong/missing client and ordinal actor/client mismatch on both routes. Denials use safe ProblemDetails without answer fields; fresh contexts verify unchanged questions/categories/options. |
| Persistence and deterministic isolation | Existing repository/migration tests cover round trips, constraints, no tracking, cancellation and competing insert IDs. New fixture guards the exact generated database/server before migration or deletion; comparisons use IDs and stored option positions rather than randomized selection order. |
| React generation, bounds, errors and answer secrecy | Existing `QuestionBankPage.test.tsx`, lifecycle tests and `src/api/questionbank.test.ts` retained and executed. They cover generation/regeneration, min/max/invalid counts, safe mapping/rendering, error/retry/auth states, stale responses, cancellation and session replacement. No superficial duplicate UI tests added. |
| Ownership and policies | Test-only change: no production code, authorization policies, contracts, dependencies, lockfile or existing tests changed. QuestionBank has no player-owned private resource; applicable isolation is public user versus machine answer access under the later approved authentication design. No new subject allowlist policy introduced. |

### Actual commands and results

Implementation baseline and focused checks (2026-11-04 local date):

| Command | Result |
| --- | --- |
| `dotnet restore QuizArena.sln --source C:\Users\kilda\.nuget\packages --disable-parallel -m:1 --verbosity minimal` | PASS, existing offline package cache. |
| `dotnet build QuizArena.sln --no-restore -warnaserror -m:1 --verbosity minimal` | PASS, 0 warnings/errors before edits. |
| `dotnet test tests/QuizArena.QuestionBank.UnitTests/QuizArena.QuestionBank.UnitTests.csproj --no-build --verbosity minimal` | Baseline 70 passed, 0 failed/skipped. |
| `dotnet test tests/QuizArena.QuestionBank.IntegrationTests/QuizArena.QuestionBank.IntegrationTests.csproj --no-build --verbosity minimal` | Baseline 140 passed; after additions 162 passed, 0 failed/skipped. |
| Client `npm ci --no-audit --no-fund` | PASS, 254 locked packages installed. |
| Client `npm run test -- --run src/features/questionbank src/api/questionbank.test.ts` | PASS, 62 tests across 3 files. |
| Focused integration project build with `--no-restore -warnaserror -m:1` | PASS, 0 warnings/errors. |

The 22 new cases passed on first execution. No production fix or test weakening was needed. Final independent validation ran against refreshed milestone base `a5f973f3caa2c79a203db735d98d3d78ff33dbb9` plus these tests. All automated code gates passed:

| Final gate | Actual result |
| --- | --- |
| Offline restore, warnings-as-errors solution build, format verification | Each exit 0; build 0 warnings/errors. |
| Full backend regression | 1,447 passed across 15 assemblies; 0 failed/skipped. |
| Separate XPlat coverage test run | 1,447 passed across 15 assemblies; 0 failed/skipped; 15 distinct Cobertura attachments. No unmeasured percentage claim. |
| Focused architecture | 21 passed; 0 failed/skipped. |
| Contract validator | PASS: 30 operations, 179 examples, 12 corruption probes. |
| Locked frontend install | Exit 0; 254 packages installed, lockfile unchanged. |
| Frontend typecheck, lint, production build | Each exit 0; lint 0 warnings. |
| Full frontend tests | 304 passed across 25 files; 0 failed/skipped. |
| Independent focused QuestionBank tests | 70 unit, 162 integration, 62 frontend passed; 0 failed/skipped. |

Commands are recorded in the scoped plan: solution restore/build/format, `dotnet test QuizArena.sln --no-build -m:1 --verbosity normal`, a separate run with `--collect:"XPlat Code Coverage"`, focused architecture and `node contracts/validate-contracts.mjs`; client `npm ci --no-audit --no-fund`, typecheck, lint, test `-- --run` and build. No source/test fixes or repair cycles were needed. Read-only review passed for draft publication with no actionable findings; independent human approval remains pending. The final PR links that review verdict and the live acceptance checklist. Local raw logs are ignored under `.dispatcher/runs/43-20261104/` and are not committed.

### Live acceptance and final human gate

Current probes of configured provider discovery (`localhost:8080/realms/quizarena/.well-known/openid-configuration`), QuestionBank health (`localhost:5103/health`) and frontend (`localhost:5173`) each timed out after 4 seconds. Docker is unavailable, and the current frontend package has no `test:e2e` script. Signed fixture tokens establish actual JWT pipeline behavior; they are not provider-backed browser acceptance. No new live acceptance is claimed.

Before closure, record these results using the configured live environment without publishing credentials:

- [ ] Sign in through the real provider and generate default/min/max quizzes; inspect public network JSON and rendered UI for answer secrecy.
- [ ] Exercise invalid count and safe failure/retry/authentication behavior against the real API.
- [ ] Verify a browser principal cannot access internal answers and the authorized configured Gameplay service client can obtain exactly five snapshots.
- [ ] Owner inspects scope, persistence, security and actual validation evidence; independent human reviewer approves the draft PR with applicable CI evidence.

Agent-reviewed research/plan and automated checks do not imply human approval. Human signoff remains pending; do not mark #43 Done or close it solely because tests or a draft PR exist.
