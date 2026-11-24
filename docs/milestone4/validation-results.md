# Milestone 4 validation results

Issue [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148) (SEC-D3). The commands and expected
outcomes are fixed. The automated checks (build, format, tests, contracts, client) have been run on the final
revision, `main` at `14ac63e` (see [Final revision](#final-revision-main-at-14ac63e)). The live journeys, the
#145 browser check, SEC-09 two-device sign-out and applying the `TightenProfilePictureContentTypeCheck`
migration to a running database were run on the same revision on 2026-11-24 (see
[Live run](#live-run-main-at-14ac63e)). The earlier candidate `68b7ecd` results below are kept as history; for
`14ac63e` the final-revision table supersedes them. Evidence observed on `46f847b` (#145 screenshots, #147
journey run) is recorded in its own column and is not a result for either revision.

## Final revision (main at 14ac63e)

| Field | Value |
|---|---|
| Commit | `14ac63ef58a003170b7abd8c4d1092a087038323` on `main` (merge of milestone PR [#166](https://github.com/JimRashed/Web-3-Final-Project/pull/166); includes [#164](https://github.com/JimRashed/Web-3-Final-Project/pull/164) and [#167](https://github.com/JimRashed/Web-3-Final-Project/pull/167)) |
| Date run | 2026-11-24, 01:05:18Z to 01:22:12Z (UTC) |
| Runner | Dispatcher validate stage (agent), automated same-revision run on a clean worktree at the exact SHA |
| Restore, build, format | `dotnet restore` exit 0; `dotnet build -warnaserror` exit 0, 0 warnings, 0 errors; `dotnet format --verify-no-changes` clean |
| Contracts | `node contracts/validate-contracts.mjs` PASS: 32 operations, 201 examples, 24 probes |
| Client | `npm ci` exit 0 (9 moderate audit advisories; not a gate); typecheck 0; lint 0; Vitest 25/25 files, 318/318 tests; build 0 |
| CI | [run 45297141502](https://github.com/JimRashed/Web-3-Final-Project/actions/runs/45297141502) on `14ac63e`: success (read with `gh run view`) |
| Live checks | Not run in this automated window (Docker was not running); run later on the same SHA, see [Live run](#live-run-main-at-14ac63e) |

`dotnet test`, one project at a time, all exit 0. `QuizArena.TestSupport` has no tests.
**Total: 1,962 passed, 0 failed, 0 skipped.**

| Project | Passed | Project | Passed |
|---|---|---|---|
| ArchitectureTests | 28 | Gameplay.IntegrationTests | 187 |
| Gameplay.UnitTests | 81 | Identity.IntegrationTests | 365 |
| Identity.UnitTests | 127 | Lobby.IntegrationTests | 302 |
| Lobby.UnitTests | 174 | Notification.IntegrationTests | 154 |
| Notification.UnitTests | 29 | QuestionBank.IntegrationTests | 173 |
| QuestionBank.UnitTests | 70 | Statistics.IntegrationTests | 223 |
| Statistics.UnitTests | 49 | | |

## Live run (main at 14ac63e)

| Field | Value |
|---|---|
| Commit | `14ac63ef58a003170b7abd8c4d1092a087038323`, clean detached worktree of `main` |
| Date run | 2026-11-24, about 03:51Z to 04:57Z (UTC) |
| Runner | Dispatcher (agent), authorized by the repository owner |
| OS, tools | Windows 11; Docker 29.1.5; Microsoft Edge (headless, Playwright) for the browser check |
| Start | `./scripts/start-milestone3.ps1 -ProvisionUsers` reached "Ready." (about 03:51Z to 03:53Z). Health ok: Identity :5101, Statistics :5102, QuestionBank :5103, Lobby :5104, Gameplay :5105, Notification :5106, Keycloak :9000 `/health/ready`, client :5173. The demo players already existed; the script reset their passwords |
| Migrations | Identity applied `AddProfilePictures`, `AddProfilePictureCheckConstraints` and `TightenProfilePictureContentTypeCheck` ("Done."); `dotnet ef migrations list` afterwards shows none pending |
| Stop | `./scripts/stop-milestone3.ps1` and `./scripts/stop-milestone3.ps1 -Keycloak`; no listeners left on the ports above |

| Check | Result |
|---|---|
| `npm run journey:api` | **10/10 PASS** (rerun 04:54:18Z). The first run stopped at 3/8: the laptop entered Modern Standby from 03:55:18Z to 04:52:35Z (Kernel-Power events 506/507) during the run, the user tokens expired and later calls returned 401. Environmental, not a product failure |
| `npm run journey:ui` | **11/11 PASS** (04:52:48Z) |
| `npm run journey:picture` | **17/17 PASS** (04:53:08Z). Observed statuses were exactly 404 (no picture), 415 (unsupported type), 400 (mismatched magic), 413 (2 MiB + 1) and 401 (unauthenticated); private headers present; version 1 → 2; the third account gets 404 for A's picture and cannot change it; a rejected upload keeps the previous picture |
| #145 browser check and SEC-09 two-device sign-out | **23/23 PASS** (04:56:35Z). Throwaway Playwright script (not committed), headless Edge, two independent browser contexts signed in as `player-a` (A and B). Details in [#145 manual check](#145-manual-check) and [#147 cross-device journey](#147-cross-device-journey) |

**Residual (SEC-09, documented risk).** After A signed out, A's previously issued access token was still
accepted by the API (HTTP 200) with about 291 s left until it expired. The APIs have no access-token
revocation list; this matches the token-lifetime residual in the
[threat model](../milestone3/threat-model.md#residual-risks-and-design-decisions).

**Runbook notes from this run.**

- Under Windows PowerShell 5.1, redirecting the start script's output with `*>&1` turns a docker stderr
  warning into a terminating error: a first start attempt exited 1 for that reason only. Run the script
  without redirecting all streams.
- After applying pending migrations the start script still prints "database is up to date". The wording is
  misleading; the migrations were applied (confirmed with `dotnet ef migrations list`).
- Keep the machine awake during the journeys: sleeping longer than the 300 s access-token lifetime makes the
  API journey fail with 401.

Screenshots (checked: no tokens or passwords visible):
[A PNG uploaded](evidence/148-01-A-png-uploaded.png),
[A after reload, before sign-in](evidence/148-02a-A-after-reload-before-sso.png),
[A after reload and SSO sign-in](evidence/148-02-A-png-after-reload.png),
[A JPEG replaced](evidence/148-03-A-jpeg-replaced.png),
[B sees the current picture](evidence/148-04-B-sees-current-picture.png),
[A after sign-out](evidence/148-05-A-after-sign-out.png),
[A profile route signed out](evidence/148-06-A-profile-route-signed-out.png),
[A sign-in asks for credentials](evidence/148-06b-A-sign-in-asks-for-credentials.png),
[B still signed in](evidence/148-07-B-still-signed-in.png).

## Earlier candidate revision (68b7ecd)

The sections from here to [CI](#ci) record the run on the earlier candidate. They are history, not results
for `14ac63e`.

| Field | Value |
|---|---|
| Commit | `68b7ecde2c534c97478c3ba3b21e8160b97e870d` (merge of [PR #165](https://github.com/JimRashed/Web-3-Final-Project/pull/165); includes #163) |
| Branch | `feature/milestone-4-security-and-cross-device-profiles` |
| Date run (automated checks) | 2026-11-23, 12:33:15Z to 13:34:37Z (UTC) |
| Runner (automated checks) | Dispatcher validate stage (agent), on a throwaway worktree checked out at the exact candidate SHA |
| OS, tools | Windows 11 10.0.26200; .NET SDK 10.0.401; Node v22.20.0, npm 10.9.3; SQL Server LocalDB `MSSQLLocalDB` 17.0.4025.3 for the integration projects |
| Edge version, live runs | Not run |

Results from different revisions are not combined: the `68b7ecd` numbers below and the `14ac63e` numbers
(automated and live) above are separate runs.

## Build and static checks

Run from the repository root.

| # | Command | Expected | Result |
|---|---|---|---|
| 1 | `dotnet restore QuizArena.sln` | Restore succeeds | Exit 0 (12:33:15Z) |
| 2 | `dotnet build QuizArena.sln --no-restore -warnaserror` | 0 warnings, 0 errors | Exit 0; 0 warnings, 0 errors (12:33:21Z) |
| 2a | `dotnet format QuizArena.sln --verify-no-changes --no-restore` | No changes needed | Exit 0 (12:33:49Z) |
| 3 | `node contracts/validate-contracts.mjs` | All contracts valid | Exit 0; PASS: 32 operations, 201 examples, 24 corruption probes (12:37:35Z) |

## Automated tests (`dotnet test`)

Integration projects create and drop their own SQL Server databases. The variable that picks the server
is read by the shell that runs `dotnet test`, not by the start script:

- **Default: Windows LocalDB.** With no variable set, the fixtures connect to `(localdb)\MSSQLLocalDB`.
  The recorded same-revision run below used LocalDB.
- **Another SQL Server:** set `QUIZARENA_TEST_SQLSERVER` to a connection string **without** a database
  name in the same shell before `dotnet test` (placeholder values; never commit a real password):

  ```powershell
  $env:QUIZARENA_TEST_SQLSERVER = 'Server=localhost,1433;User ID=sa;Password=<your-password>;TrustServerCertificate=True'
  ```

  ```bash
  export QUIZARENA_TEST_SQLSERVER='Server=localhost,1433;User ID=sa;Password=<your-password>;TrustServerCertificate=True'
  ```

- `QUIZARENA_SQLSERVER` is different: `./scripts/start-milestone3.ps1` uses it to point the **running APIs**
  at a server ([runbook](../milestone3/runbook.md#prerequisites)). Setting it does not change where the tests run.
- **CI** starts a SQL Server container per job, masks its generated password and exports
  `QUIZARENA_TEST_SQLSERVER` to the test step ([runbook](../milestone3/runbook.md#ci)).

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
| `14ac63e` (`main`) | [run 45297141502](https://github.com/JimRashed/Web-3-Final-Project/actions/runs/45297141502) | success (read with `gh run view`; head SHA `14ac63e`) |

## Live end-to-end journeys

Needs Docker, the local stack and Microsoft Edge.

```powershell
./scripts/start-milestone3.ps1
cd tests/e2e; npm ci; npm run journey:api; npm run journey:ui; npm run journey:picture; cd ../..
./scripts/stop-milestone3.ps1
```

| # | Command | Expected | Result |
|---|---|---|---|
| 22 | `npm run journey:api` | PASS | Not run on `68b7ecd`. On `14ac63e`: 10/10 PASS ([live run](#live-run-main-at-14ac63e)) |
| 23 | `npm run journey:ui` | PASS | Not run on `68b7ecd`. On `14ac63e`: 11/11 PASS |
| 24 | `npm run journey:picture` | PASS on a fresh stack (see [#147](#147-cross-device-journey)) | Not run on `68b7ecd`. On `14ac63e`: 17/17 PASS; 17/17 PASS also recorded on `46f847b` + #163 |

## #145 manual check

Steps from SuSu-Lisu's [#145 comment](https://github.com/JimRashed/Web-3-Final-Project/issues/145) of 2026-11-23
05:04:59. Screenshots from a live run on `46f847b` (stack from `scripts/start-milestone3.ps1`, signed in as
`player-b`) were merged in [PR #165](https://github.com/JimRashed/Web-3-Final-Project/pull/165) and linked in the
[closing comment](https://github.com/JimRashed/Web-3-Final-Project/issues/145#issuecomment-7092450934) (05:29:24; issue closed 05:29:25).
That is a different revision from the candidate, and PR #165 merged with changes requested
([review 6016941081](https://github.com/JimRashed/Web-3-Final-Project/pull/165#pullrequestreview-6016941081)).

| Step | Action | Expected | Observed on `46f847b` (#165) | Result on `14ac63e` (live, 2026-11-24; not run on `68b7ecd`) |
|---|---|---|---|---|
| 1 | `./scripts/start-milestone3.ps1`, open `http://localhost:5173`, sign in | Signed in, profile reachable | Signed in as `player-b` | PASS: signed in as `player-a` in two independent contexts (A, B) |
| 2 | Profile → Choose picture → a PNG; reload | Thumbnail shown and survives a reload | [145-display.png](evidence/145-display.png): picture rendered, "Picture saved."; reload not shown | PASS: rendered as `<img src="blob:...">` from an `image/png` response ([148-01](evidence/148-01-A-png-uploaded.png)). After a reload the page is anonymous because the client keeps tokens in memory ([148-02a](evidence/148-02a-A-after-reload-before-sso.png)); Sign in re-enters through the Keycloak SSO session and the picture is fetched again ([148-02](evidence/148-02-A-png-after-reload.png)) |
| 3 | Replace it with a JPEG | Old thumbnail replaced; `GET /api/users/me/picture` returns the new bytes with `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff` | Not evidenced by the screenshots. The headers were observed by the API journey (#147 run below), not in the browser | PASS: the authorized GET carried `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff`; the JPEG appears under a new blob URL and the previous object URL was revoked ([148-03](evidence/148-03-A-jpeg-replaced.png)) |
| 4 | Sign out | Panel disappears; nothing image-related remains in the page | [145-signed-out.png](evidence/145-signed-out.png): panel gone, anonymous page; "nothing remains" not evidenced beyond the view | PASS: panel gone and Sign in shown ([148-05](evidence/148-05-A-after-sign-out.png)); `/profile` shows sign-in required ([148-06](evidence/148-06-A-profile-route-signed-out.png)); 0 picture GETs by A after sign-out |
| — | Refused file (error state) | Validation message; previous picture still shown | [145-error.png](evidence/145-error.png): local pre-check refuses `not-an-image.txt`; a server-side rejection is not shown | Not run in the browser; server-side rejections (415, 400, 413) with the previous picture kept passed in `journey:picture` on `14ac63e` |
| — | Missing state (no picture) | Empty state | Not captured; covered by `ProfilePage.test.tsx` and the API journey's 404 ([evidence README](evidence/145-README.md)) | Not run in the browser; 404 for a player without a picture passed in `journey:picture` on `14ac63e` |

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

| Check | Expected | Observed (`46f847b` + #163) | Result on `14ac63e` (live, 2026-11-24; not run on `68b7ecd`) |
|---|---|---|---|
| `npm run journey:picture` on a fresh stack | PASS output pasted with the commit it ran on | 17/17 PASS; SHA and database state not stated | 17/17 PASS on the stated SHA; demo users provisioned by `start-milestone3.ps1 -ProvisionUsers` on the existing local stack (not a fresh database) |
| A and B sign in separately; tokens differ | Independent sessions | PASS | PASS |
| A uploads; B reads `GET /api/users/me` then the picture | B sees A's `picture.version` and `contentType`, identical bytes | Bytes PASS; profile metadata on B not checked | Bytes PASS; the script still does not compare B's profile metadata |
| A replaces; B reads again | B sees the new version and bytes | PASS (version 1 → 2 seen on A; B sees the replacement) | PASS (version 1 → 2; B sees the replacement) |
| Rejected uploads (wrong type, mismatched magic, 2 MiB + 1) | 415, 400, 413; previous picture unchanged | PASS (415, 400, 413; previous picture kept) | PASS (exactly 415, 400, 413; previous picture kept) |
| SEC-09: A signs out | A's local state and picture cleared; B's next profile and picture reads still succeed | **Not covered** | **PASS** in the browser check (see below); residual: A's issued access token still accepted until expiry |

**SEC-09 manual steps (only if not automated; proposed, to be confirmed by the #147 owner).** Sign in as the
same player in two separate Edge windows (one normal, one InPrivate) as A and B. On A, sign out from the app
and complete the Keycloak logout redirect. Expected: A shows the signed-out view with no profile or picture
and needs a new sign-in; B, without signing in again, reloads Profile and still shows the current picture.
Result on `14ac63e` (2026-11-24, 04:56:35Z; not run on `68b7ecd`): run as an automated browser check
(Playwright, headless Edge, two independent contexts instead of a normal and an InPrivate window), 23/23 PASS.
After A signed out, the profile panel was gone and Sign in was shown, `/profile` showed sign-in required,
A made 0 picture requests afterwards, A's Keycloak SSO session had ended (Sign in showed the login form
again, [148-06b](evidence/148-06b-A-sign-in-asks-for-credentials.png)) and an unauthenticated picture GET
returned 401. B stayed signed in, fetched the current picture again and, after a reload, re-entered through
its own SSO session ([148-04](evidence/148-04-B-sees-current-picture.png),
[148-07](evidence/148-07-B-still-signed-in.png)). Residual: A's previously issued access token was still
accepted (HTTP 200) with about 291 s left until expiry (see [Live run](#live-run-main-at-14ac63e)). The
journey script itself still does not automate this step.

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
