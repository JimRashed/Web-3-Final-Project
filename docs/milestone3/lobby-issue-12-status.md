# Lobby issue #12: partial rollup status

As of 2026-11-04, the user authorized completing eligible Lobby work while leaving unresolved hard prerequisites alone. One draft PR is intended for the implemented work. Human signoff is pending; this document does not claim issue closure or live acceptance.

## Child stages and acceptance

Each implemented child received its own genuine sequential Prime, Plan, Implement, Testing, Validate and read-only Code Review stages on `feature/m3/12-lobby-service` (fetched milestone base `4f5d8cb`).

| Child | Stage outcome | Repair cycles | Actual acceptance status |
|---|---|---:|---|
| [#44 Domain](https://github.com/JimRashed/Web-3-Final-Project/issues/44) | All six stages passed | 0 | Implemented; 66 Domain tests; independent human review pending |
| [#45 Persistence](https://github.com/JimRashed/Web-3-Final-Project/issues/45) | All six stages passed | 0 | Implemented; 39 focused real SQL Server cases; human review pending |
| [#46 Application](https://github.com/JimRashed/Web-3-Final-Project/issues/46) | All six stages passed | 0 | Implemented; 174 Lobby unit tests and 21 architecture cases; human review pending |
| [#47 REST/security](https://github.com/JimRashed/Web-3-Final-Project/issues/47) | All six stages passed | 1 | Implemented; 277 Lobby integration tests; trace-ID contract finding fixed and revalidated; human review pending |
| [#48 React](https://github.com/JimRashed/Web-3-Final-Project/issues/48) | All six stages passed for eligible implementation | 2 | UI/public REST implementation checked; live Gameplay/guest acceptance remains blocked by #53 |
| [#49 Vertical-slice audit](https://github.com/JimRashed/Web-3-Final-Project/issues/49) | Not started | 0 | Hard prerequisite #24 remains unresolved; no stage or audit completion claimed |

Plans: [parent #12](../../specs/issue-12-plan.md), [#44](../../specs/issue-44-plan.md), [#45](../../specs/issue-45-plan.md), [#46](../../specs/issue-46-plan.md), [#47](../../specs/issue-47-plan.md), [#48](../../specs/issue-48-plan.md). Test counts above are stage evidence, not counts to add together; the aggregate totals below include them.

## Implemented behavior

The owned service enforces two distinct subjects including the host, Open-only idempotent joins, capacity conflicts and host-only locking. Data-only EF rows map through checked immutable Domain rehydration. Owned SQL Server tables enforce unique codes/memberships, cascade deletion and rowversion concurrency; membership writes update/check the parent version atomically, with bounded collision/reload retries.

The five REST routes use genuine bearer policies, trusted-principal identity, safe public/internal DTOs and contract-shaped errors. Disposable SQL Server and actual HTTP tests verify persisted outcomes. Active legacy aggregate code remains in place for its separately scoped retirement.

The React Lobby page provides accessible create/join/copy controls, caller-relative membership/state, host lock/start controls, cancellation, sequential polling and stale-result protection. Gameplay start uses its public origin and `{joinCode}` only. Returned opaque match IDs navigate through `/matches?matchId=...`; the same-origin copied link preserves exact IDs, including dots, percent encodings, controls and Unicode. The match/notification destinations remain shell placeholders; peer screens and guest event flows are incomplete.

## Current executed validation

Before publication, the branch was rebased without conflicts onto the freshly fetched milestone base `d007380`. All 69 reviewed Lobby file contents were preserved. Root reran restore/build/format, full backend tests and coverage on that combined tree; earlier per-child counts remain stage evidence.

| Check | Actual result |
|---|---|
| `dotnet restore QuizArena.sln` | Exit 0 |
| `dotnet build QuizArena.sln --no-restore -warnaserror -m:1` | Exit 0; zero warnings/errors |
| `dotnet format QuizArena.sln --verify-no-changes --no-restore` | Exit 0 |
| `dotnet test QuizArena.sln --no-build --verbosity normal -m:1` | Exit 0; 957 passed across 14 nonempty assemblies, zero failed/skipped |
| `dotnet test QuizArena.sln --no-build --collect:"XPlat Code Coverage" -m:1` | Exit 0; 957 passed; 15 coverage XML attachments verified; no aggregate percentage claimed |
| Client `npm ci`, `npm run typecheck`, `npm run lint`, `npm run test -- --run`, `npm run build` | All exit 0; 173 tests across 12 files, zero failed/skipped and zero lint warnings |
| `node contracts/validate-contracts.mjs` | Exit 0; 30 operations, 179 examples, 12 corruption probes; limited schema/invariant checker |
| `git diff --check` | Exit 0 |

Root executed the .NET and frontend gates. Validate/Code Review independently inspected the child-stage evidence (870 backend and 173 frontend passes); root then executed and checked the post-rebase backend results shown above. Workers executed the offline contract checker and whitespace checks. Raw local logs and coverage outputs remain untracked. Frontend source did not change during synchronization, so its passing results remain current. The unrelated QuestionBank unit scaffold discovers zero tests and contributes no passes. `npm ci` reports 11 moderate advisories in unchanged dependencies; no package/lockfile upgrades are included.

Review corrections are complete: #47's trace ID now obeys the existing 100-character schema limit; #48 uses a query destination instead of normalizable dot path segments and accepts the exact nonblank, maximum 100-Unicode-code-point MatchId contract. All corresponding regressions passed. Initial implementation failures and infrastructure EPERM failures remain recorded locally, separately from successful current evidence.

## Remaining work and board meaning

- [#24](https://github.com/JimRashed/Web-3-Final-Project/issues/24) remains open/In progress, owned by SuSu-Lisu, with unmerged [draft PR #83](https://github.com/JimRashed/Web-3-Final-Project/pull/83). #49 remains unstarted behind this hard prerequisite; its prescribed final vertical-slice audit and `validation-results.md` work were not performed as #49.
- [#53](https://github.com/JimRashed/Web-3-Final-Project/issues/53) remains open/Backlog behind #52. #48's real Gameplay start/navigation and complete guest/live acceptance remain unverified. Fetch doubles prove contract-based integration behavior, not live deployment or Keycloak/Gameplay acceptance.
- [#12](https://github.com/JimRashed/Web-3-Final-Project/issues/12) remains in progress. #44–47 are implemented and await independent human review; #48 is partial with live acceptance blocked; #49 is unstarted/blocked. These distinctions must be reflected on the project board, without marking incomplete issues Done.

The authenticated dependency recheck used logged-in Chrome and immediately released control. This status document records the implementation handoff; the root agent publishes the PR, and the user owns further issue/board updates per their latest instruction. Do not use closing references for #12, #48 or #49 while their acceptance is incomplete. No merge, manual issue closure or human approval is claimed.
