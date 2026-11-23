# Milestone 4 validation results

Issue [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148) (SEC-D3). The commands and expected
outcomes are fixed. The automated checks (build, format, tests, contracts, client) have been run on the
candidate; the live journeys, the #145 browser check and SEC-09 stay `pending` until someone runs them with the
stack (Phase B of [the plan](../../specs/issue-148-plan.md)). Evidence observed on the earlier revision `46f847b`
(#145 screenshots, #147 journey run) is recorded in its own column and is not a result for the candidate.

## Candidate revision

| Field | Value |
|---|---|
| Commit | `68b7ecde2c534c97478c3ba3b21e8160b97e870d` (merge of [PR #165](https://github.com/JimRashed/Web-3-Final-Project/pull/165); includes #163) |
| Branch | `feature/milestone-4-security-and-cross-device-profiles` |
| Date run (automated checks) | 2026-11-23, 12:33:15Z to 13:34:37Z (UTC) |
| Runner (automated checks) | Dispatcher validate stage (agent), on a throwaway worktree checked out at the exact candidate SHA |
| OS, tools | Windows 11 10.0.26200; .NET SDK 10.0.401; Node v22.20.0, npm 10.9.3; SQL Server LocalDB `MSSQLLocalDB` 17.0.4025.3 for the integration projects |
| Edge version, live runs | pending |

If the branch moves past `68b7ecd` before the runs, update the commit here and rerun everything; results
from different revisions are not combined.

## Build and static checks

Run from the repository root.

| # | Command | Expected | Result |
|---|---|---|---|
| 1 | `dotnet restore QuizArena.sln` | Restore succeeds | Exit 0 (12:33:15Z) |
| 2 | `dotnet build QuizArena.sln --no-restore -warnaserror` | 0 warnings, 0 errors | Exit 0; 0 warnings, 0 errors (12:33:21Z) |
| 2a | `dotnet format QuizArena.sln --verify-no-changes --no-restore` | No changes needed | Exit 0 (12:33:49Z) |
| 3 | `node contracts/validate-contracts.mjs` | All contracts valid | Exit 0; PASS: 32 operations, 201 examples, 24 corruption probes (12:37:35Z) |

## Automated tests (`dotnet test`)

Integration projects need SQL Server from the local stack (`./scripts/start-milestone3.ps1`), LocalDB or CI.
Run one project at a time, 12:34:34Z to 12:37:19Z, all exit 0. `QuizArena.TestSupport` has no tests.
**Total: 1,953 passed, 0 failed, 0 skipped.**

| # | Command | Expected | Result |
|---|---|---|---|
| 4 | `dotnet test tests/QuizArena.ArchitectureTests --no-build` | All pass | Exit 0; 28 passed, 0 failed |
| 5 | `dotnet test tests/QuizArena.Gameplay.UnitTests --no-build` | All pass | Exit 0; 81 passed, 0 failed |
| 6 | `dotnet test tests/QuizArena.Gameplay.IntegrationTests --no-build` | All pass | Exit 0; 187 passed, 0 failed |
| 7 | `dotnet test tests/QuizArena.Identity.UnitTests --no-build` | All pass | Exit 0; 127 passed, 0 failed |
| 8 | `dotnet test tests/QuizArena.Identity.IntegrationTests --no-build` | All pass | Exit 0; 356 passed, 0 failed |
| 9 | `dotnet test tests/QuizArena.Lobby.UnitTests --no-build` | All pass | Exit 0; 174 passed, 0 failed |
| 10 | `dotnet test tests/QuizArena.Lobby.IntegrationTests --no-build` | All pass | Exit 0; 302 passed, 0 failed |
| 11 | `dotnet test tests/QuizArena.Notification.UnitTests --no-build` | All pass | Exit 0; 29 passed, 0 failed |
| 12 | `dotnet test tests/QuizArena.Notification.IntegrationTests --no-build` | All pass | Exit 0; 154 passed, 0 failed |
| 13 | `dotnet test tests/QuizArena.QuestionBank.UnitTests --no-build` | All pass | Exit 0; 70 passed, 0 failed |
| 14 | `dotnet test tests/QuizArena.QuestionBank.IntegrationTests --no-build` | All pass | Exit 0; 173 passed, 0 failed |
| 15 | `dotnet test tests/QuizArena.Statistics.UnitTests --no-build` | All pass | Exit 0; 49 passed, 0 failed |
| 16 | `dotnet test tests/QuizArena.Statistics.IntegrationTests --no-build` | All pass | Exit 0; 223 passed, 0 failed |

## Client (`src/QuizArena.Client`)

```powershell
cd src/QuizArena.Client; npm ci; npm run typecheck; npm run lint; npx vitest run; npm run build; cd ../..
```

| # | Step | Expected | Result |
|---|---|---|---|
| 17 | `npm ci` | Clean install | Exit 0 (9 moderate audit advisories reported; not a gate) |
| 18 | `npm run typecheck` | No errors | Exit 0 |
| 19 | `npm run lint` | 0 warnings (`--max-warnings 0`) | Exit 0 |
| 20 | `npx vitest run` | All pass | First attempt exit 1: 11 of 25 files failed to start workers ("[vitest-pool]: Failed to start forks worker … Timeout waiting for worker to respond"); 115 tests ran, 0 failed (environmental). One rerun at 13:34:37Z: exit 0, 25/25 files, 318/318 tests passed |
| 21 | `npm run build` | Build succeeds | Exit 0 |

## CI

Workflow [`ci.yml`](../../.github/workflows/ci.yml): job `backend` (restore, build, SQL Server, `dotnet test`)
and job `frontend` (typecheck, lint, Vitest, build). There is **no e2e job** in CI, so the journeys below
are manual evidence.

| Commit | Run | Conclusion |
|---|---|---|
| `46f847b` | [run 45115561119](https://github.com/JimRashed/Web-3-Final-Project/actions/runs/45115561119) | success (read with `gh run view` on 2026-11-23; head SHA `46f847b`) |
| `68b7ecd` | [run 45118703355](https://github.com/JimRashed/Web-3-Final-Project/actions/runs/45118703355) | success (`pull_request` event for #166; read with `gh run view` on 2026-11-23) |
| `68b7ecd` | [run 45118087469](https://github.com/JimRashed/Web-3-Final-Project/actions/runs/45118087469) | success (read with `gh run list` on 2026-11-23; head SHA `68b7ecd`). Run 45118083732 on `94bc2e3` was cancelled when `68b7ecd` superseded it |

## Live end-to-end journeys

Needs Docker, the local stack and Microsoft Edge.

```powershell
./scripts/start-milestone3.ps1
cd tests/e2e; npm ci; npm run journey:api; npm run journey:ui; npm run journey:picture; cd ../..
./scripts/stop-milestone3.ps1
```

| # | Command | Expected | Result |
|---|---|---|---|
| 22 | `npm run journey:api` | PASS | pending |
| 23 | `npm run journey:ui` | PASS | pending |
| 24 | `npm run journey:picture` | PASS on a fresh stack (see [#147](#147-cross-device-journey)) | pending on `68b7ecd`; 17/17 PASS recorded on `46f847b` + #163 |

## #145 manual check

Steps from SuSu-Lisu's [#145 comment](https://github.com/JimRashed/Web-3-Final-Project/issues/145) of 2026-11-23
05:04:59. Screenshots from a live run on `46f847b` (stack from `scripts/start-milestone3.ps1`, signed in as
`player-b`) were merged in [PR #165](https://github.com/JimRashed/Web-3-Final-Project/pull/165) and linked in the
[closing comment](https://github.com/JimRashed/Web-3-Final-Project/issues/145#issuecomment-7092450934) (05:29:24; issue closed 05:29:25).
That is a different revision from the candidate, and PR #165 merged with changes requested
([review 6016941081](https://github.com/JimRashed/Web-3-Final-Project/pull/165#pullrequestreview-6016941081)).

| Step | Action | Expected | Observed on `46f847b` (#165) | Result on `68b7ecd` |
|---|---|---|---|---|
| 1 | `./scripts/start-milestone3.ps1`, open `http://localhost:5173`, sign in | Signed in, profile reachable | Signed in as `player-b` | pending |
| 2 | Profile → Choose picture → a PNG; reload | Thumbnail shown and survives a reload | [145-display.png](evidence/145-display.png): picture rendered, "Picture saved."; reload not shown | pending |
| 3 | Replace it with a JPEG | Old thumbnail replaced; `GET /api/users/me/picture` returns the new bytes with `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff` | Not evidenced by the screenshots. The headers were observed by the API journey (#147 run below), not in the browser | pending |
| 4 | Sign out | Panel disappears; nothing image-related remains in the page | [145-signed-out.png](evidence/145-signed-out.png): panel gone, anonymous page; "nothing remains" not evidenced beyond the view | pending |
| — | Refused file (error state) | Validation message; previous picture still shown | [145-error.png](evidence/145-error.png): local pre-check refuses `not-an-image.txt`; a server-side rejection is not shown | pending |
| — | Missing state (no picture) | Empty state | Not captured; covered by `ProfilePage.test.tsx` and the API journey's 404 ([evidence README](evidence/145-README.md)) | pending |

## #147 cross-device journey

Script: [`tests/e2e/picture-journey.mjs`](../../tests/e2e/picture-journey.mjs), merged in
[PR #160](https://github.com/JimRashed/Web-3-Final-Project/pull/160) and fixed in
[PR #163](https://github.com/JimRashed/Web-3-Final-Project/pull/163) (saves a display name before the first picture).

**Recorded run (e2e evidence for #147).** Pasted by SuSu-Lisu in the
[#147 closing comment](https://github.com/JimRashed/Web-3-Final-Project/issues/147#issuecomment-7092450545)
(issue closed 05:29:23):

| Field | Value |
|---|---|
| Result | **17/17 checks passed** |
| Revision | milestone-4 branch at `46f847b` "with the fix in #163 merged" (exact head SHA not stated; not `68b7ecd`) |
| Stack | Keycloak + six APIs + client started by `scripts/start-milestone3.ps1`; whether the database was fresh is not stated |
| Earlier attempt | First run failed at the upload with 404 (no profile), which led to #163 |

PASS lines as pasted: two independent sessions plus a third account; A and B hold different tokens; both
accounts have a profile (a=200, other=200); a player without a picture gets 404; A uploads a PNG (200,
`image/png`); B retrieves the same bytes (200); private headers (`private, no-store`, `nosniff`); A replaces
and the version goes 1 → 2; B sees the replacement; a third account cannot read A's picture (404); the third
account can only replace its own picture; A's picture untouched; unauthenticated read 401; unsupported type 415;
mismatched magic 400; 2 MiB + 1 413; a rejected upload keeps the previous picture.

**Not covered by that run** ([#160 review](https://github.com/JimRashed/Web-3-Final-Project/pull/160#pullrequestreview-6016902334),
[#163 review](https://github.com/JimRashed/Web-3-Final-Project/pull/163#pullrequestreview-6016944462)): SEC-09
two-device sign-out; B reading `GET /api/users/me` to compare `picture.version` and `contentType`. The script
still accepts 415 for the mismatch case (the run printed 400), accepts 200 for a 404 check, does not assert A's
`version`/`updatedAt` after the third-account upload or the image `Content-Type`, can race on token capture, and
renames both demo accounts on every run.

| Check | Expected | Observed (`46f847b` + #163) | Result on `68b7ecd` |
|---|---|---|---|
| `npm run journey:picture` on a fresh stack | PASS output pasted with the commit it ran on | 17/17 PASS; SHA and database state not stated | pending |
| A and B sign in separately; tokens differ | Independent sessions | PASS | pending |
| A uploads; B reads `GET /api/users/me` then the picture | B sees A's `picture.version` and `contentType`, identical bytes | Bytes PASS; profile metadata on B not checked | pending |
| A replaces; B reads again | B sees the new version and bytes | PASS (version 1 → 2 seen on A; B sees the replacement) | pending |
| Rejected uploads (wrong type, mismatched magic, 2 MiB + 1) | 415, 400, 413; previous picture unchanged | PASS (415, 400, 413; previous picture kept) | pending |
| SEC-09: A signs out | A's local state and picture cleared; B's next profile and picture reads still succeed | **Not covered** | pending (automated or manual, see below) |

**SEC-09 manual steps (only if not automated; proposed, to be confirmed by the #147 owner).** Sign in as the
same player in two separate Edge windows (one normal, one InPrivate) as A and B. On A, sign out from the app
and complete the Keycloak logout redirect. Expected: A shows the signed-out view with no profile or picture
and needs a new sign-in; B, without signing in again, reloads Profile and still shows the current picture.
Result: pending.

## Historical per-PR numbers

Reported in PR bodies at the time; **not current evidence** for the candidate revision.

| PR | Reported |
|---|---|
| [#149](https://github.com/JimRashed/Web-3-Final-Project/pull/149) | 1456/1456 |
| [#152](https://github.com/JimRashed/Web-3-Final-Project/pull/152) | 1542 |
| [#155](https://github.com/JimRashed/Web-3-Final-Project/pull/155) | 1594 + contract validator |
| [#156](https://github.com/JimRashed/Web-3-Final-Project/pull/156) | 1594 |
| [#153](https://github.com/JimRashed/Web-3-Final-Project/pull/153) | 1628 + Vitest 310 + secret scan |
| [#154](https://github.com/JimRashed/Web-3-Final-Project/pull/154) | Migration rehearsal + CI |
| [#159](https://github.com/JimRashed/Web-3-Final-Project/pull/159) | 1953/1953 |
| [#162](https://github.com/JimRashed/Web-3-Final-Project/pull/162) | Identity 75/233/28 |
| [#157](https://github.com/JimRashed/Web-3-Final-Project/pull/157), [#158](https://github.com/JimRashed/Web-3-Final-Project/pull/158), [#160](https://github.com/JimRashed/Web-3-Final-Project/pull/160) | CI only |
| [#163](https://github.com/JimRashed/Web-3-Final-Project/pull/163) | 17/17 claimed, 13 lines pasted; full run on #147 |
| [#165](https://github.com/JimRashed/Web-3-Final-Project/pull/165) | Screenshots (docs only) |
| [#151](https://github.com/JimRashed/Web-3-Final-Project/pull/151) | Vitest |
