# Issue #12: Lobby service roll-up

Issue [#12](https://github.com/JimRashed/Web-3-Final-Project/issues/12), epic #7. The user requests one shared branch and one final PR for this feature and its sub-issues, and later explicitly authorized completing eligible work while leaving hard prerequisites alone. The resulting partial draft PR must clearly report remaining acceptance on the project board. Worktree: `issue-12-lobby-service`; branch: `feature/m3/12-lobby-service`; fetched milestone base: `4f5d8cb`. Caller changes remain untouched. Human approval remains pending; incomplete work is identified below.

## Architecture and sequence

Each child receives its own genuine sequential Prime → Plan → Implement → Testing → Validate → read-only Code Review loop, scoped plan and actual reports. No parallel editing or child PR publication. Internal sibling prerequisites become available on this shared branch once their loop passes; GitHub closure is not fabricated. Each child has an initial attempt plus at most two repair cycles. Root Dispatcher alone handles the final conventional commit/push and draft PR after aggregate gates.

| Child | Intended scope and owned paths | Prerequisites / status |
|---|---|---|
| #44 | Pure entities, two-player/host/state rules and code generation in `src/QuizArena.Lobby.Domain/`; tests in `tests/QuizArena.Lobby.UnitTests/Domain/` | Full Prime/Plan/Implement/Testing/Validate/Code Review loop passed; 66 owned tests and 534 solution tests passed, zero failures/skips; build/format clean; no repairs; human signoff pending |
| #45 | Owned EF model/configurations/migrations and cancellation-aware repositories in `src/QuizArena.Lobby.Data/`; relational persistence/concurrency tests | Full Prime/Plan/Implement/Testing/Validate/Code Review loop passed; 39 focused SQL tests and 571 solution tests passed, zero failures/skips; build/format clean; no repairs; human signoff pending |
| #46 | Public DTOs and trusted-identity application orchestration in `src/QuizArena.Lobby.Api/{Contracts,Services,Clients}/`; mocked application tests | Full Prime/Plan/Implement/Testing/Validate/Code Review loop passed; 174 Lobby unit, 21 architecture and 694 solution tests passed, zero failures/skips; build/format clean; no repairs; human signoff pending |
| #47 | Thin secured REST mappings, safe ProblemDetails and public OpenAPI; real bearer endpoint/security tests | Full Prime/Plan/Implement/Testing/Validate/Code Review loop passed after one trace-contract repair; 277 Lobby integration and 870 solution tests passed, zero failures/skips; build/format clean; human signoff pending |
| #48 | Lobby React page/API module, create/join/copy/lock/start controls, polling/cancellation and component tests | All six stages passed for eligible implementation after two opaque-ID contract repairs; 173 frontend tests passed, zero failures/skips; typecheck/lint/build clean; live Gameplay/guest acceptance remains blocked by external #53 |
| #49 | Coverage audit, missing unit/relational/HTTP/UI cases, persistence/isolation/security matrix and actual validation documentation | Unstarted: external #24 remains an unresolved hard start prerequisite with unmerged draft PR #83; no stage completion claimed |

Every child plan will list its exact files before edits. Preserve aggregate legacy code still serving the aggregate host until its separately scoped retirement. APIs own contracts/orchestration, Domain owns pure invariants, Data owns EF and repositories. Lobby never creates matches or accesses Gameplay tables/assemblies; the browser calls Gameplay's public REST contract after locking.

## Acceptance and validation

The roll-up must demonstrate:

- Exactly two distinct opaque subjects including host; Open-only joins, Open duplicate idempotency, capacity conflicts, host-only two-member lock and repeated host lock idempotency.
- Owned database constraints, bounded join-code collision retries, rowversion concurrency and concurrent last-slot capacity protection, verified with disposable SQL Server and fresh contexts.
- Public create/get/join/lock routes, correct 201/200/400/401/403/404/409 outcomes and Location; service-only internal verification under `lobbies.verify`.
- Safe preview DTOs, claims-derived identity, no client-selected host/player IDs, no entity leakage or cross-service persistence references.
- Connected Lobby UI with errors/loading/submission state, host-only controls, polling cleanup, copied code and public Gameplay start navigation; mocks are labeled and do not establish live acceptance.
- A separate real stage report and acceptance mapping per child, aggregate regression/security evidence, and independent human signoff pending on one final draft PR.

Required final commands from repository root:

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror -m:1
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal -m:1
dotnet test QuizArena.sln --no-build --collect:"XPlat Code Coverage" -m:1
git diff --check
```

From `src/QuizArena.Client`: `npm ci`, `npm run typecheck`, `npm run lint`, `npm run test -- --run`, `npm run build`, and the applicable provisioned live/E2E checks. Each child also runs focused owned tests and architecture gates. Record exact commands, exits, totals and environment limitations; never skip failures or reuse historical counts as current evidence.

## External boundaries and final handoff

The authenticated preflight and later logged-in Chrome recheck found #24 and Gameplay #53 open. #24 is In progress with unmerged draft PR #83; #53 is Backlog behind #52. Do not implement or close these other owners' work to manufacture completion. #48's permitted mock-based implementation loop has passed, while live acceptance remains blocked; #49 remains unstarted until #24 resolves. The user authorizes one partial draft PR for eligible implementation with these distinctions clearly reflected on the board. See [the tracked status and actual validation](../docs/milestone3/lobby-issue-12-status.md). The PR must not claim complete acceptance or use closing references for #12/#48/#49 while those requirements remain incomplete.

No behavior beyond the P02 contracts, secrets, authentication weakening, fabricated CI/human approvals, automatic merge or manual issue closure. The PR targets `feature/milestone-3-layered-service-development`, references #12, #44–#49 and #7, links scoped plans and actual review/validation evidence, and states `Human signoff: pending`.
