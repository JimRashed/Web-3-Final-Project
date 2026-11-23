# Milestone 4 validation results

Issue [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148) (SEC-D3). This is a **skeleton**:
the commands and expected outcomes are fixed, and every result cell stays `pending` until someone runs it
on the candidate revision (Phase B of [the plan](../../specs/issue-148-plan.md)). No result here is a pass
for `46f847b` unless it says so with a link.

## Candidate revision

| Field | Value |
|---|---|
| Commit | `46f847b` (merge of [PR #160](https://github.com/JimRashed/Web-3-Final-Project/pull/160)) |
| Branch | `feature/milestone-4-security-and-cross-device-profiles` |
| Date run | pending |
| Runner | pending |
| OS, .NET SDK, Node, Edge versions | pending |

If the branch moves past `46f847b` before the runs, update the commit here and rerun everything; results
from different revisions are not combined.

## Build and static checks

Run from the repository root.

| # | Command | Expected | Result |
|---|---|---|---|
| 1 | `dotnet restore QuizArena.sln` | Restore succeeds | pending |
| 2 | `dotnet build QuizArena.sln --no-restore -warnaserror` | 0 warnings, 0 errors | pending |
| 3 | `node contracts/validate-contracts.mjs` | All contracts valid | pending |

## Automated tests (`dotnet test`)

Integration projects need SQL Server from the local stack (`./scripts/start-milestone3.ps1`) or CI.

| # | Command | Expected | Result |
|---|---|---|---|
| 4 | `dotnet test tests/QuizArena.ArchitectureTests --no-build` | All pass | pending |
| 5 | `dotnet test tests/QuizArena.Gameplay.UnitTests --no-build` | All pass | pending |
| 6 | `dotnet test tests/QuizArena.Gameplay.IntegrationTests --no-build` | All pass | pending |
| 7 | `dotnet test tests/QuizArena.Identity.UnitTests --no-build` | All pass | pending |
| 8 | `dotnet test tests/QuizArena.Identity.IntegrationTests --no-build` | All pass | pending |
| 9 | `dotnet test tests/QuizArena.Lobby.UnitTests --no-build` | All pass | pending |
| 10 | `dotnet test tests/QuizArena.Lobby.IntegrationTests --no-build` | All pass | pending |
| 11 | `dotnet test tests/QuizArena.Notification.UnitTests --no-build` | All pass | pending |
| 12 | `dotnet test tests/QuizArena.Notification.IntegrationTests --no-build` | All pass | pending |
| 13 | `dotnet test tests/QuizArena.QuestionBank.UnitTests --no-build` | All pass | pending |
| 14 | `dotnet test tests/QuizArena.QuestionBank.IntegrationTests --no-build` | All pass | pending |
| 15 | `dotnet test tests/QuizArena.Statistics.UnitTests --no-build` | All pass | pending |
| 16 | `dotnet test tests/QuizArena.Statistics.IntegrationTests --no-build` | All pass | pending |

## Client (`src/QuizArena.Client`)

```powershell
cd src/QuizArena.Client; npm ci; npm run typecheck; npm run lint; npx vitest run; npm run build; cd ../..
```

| # | Step | Expected | Result |
|---|---|---|---|
| 17 | `npm ci` | Clean install | pending |
| 18 | `npm run typecheck` | No errors | pending |
| 19 | `npm run lint` | 0 warnings (`--max-warnings 0`) | pending |
| 20 | `npx vitest run` | All pass | pending |
| 21 | `npm run build` | Build succeeds | pending |

## CI

Workflow [`ci.yml`](../../.github/workflows/ci.yml): job `backend` (restore, build, SQL Server, `dotnet test`)
and job `frontend` (typecheck, lint, Vitest, build). There is **no e2e job** in CI, so the journeys below
are manual evidence.

| Commit | Run | Conclusion |
|---|---|---|
| `46f847b` | [run 45115561119](https://github.com/JimRashed/Web-3-Final-Project/actions/runs/45115561119) | success (read with `gh run view` on 2026-11-23; head SHA `46f847b`) |

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
| 24 | `npm run journey:picture` | PASS on a fresh stack (see [#147](#147-cross-device-journey)) | pending |

## #145 manual check

From SuSu-Lisu's [#145 comment](https://github.com/JimRashed/Web-3-Final-Project/issues/145) of 2026-11-23
05:04:59. Run with the stack up.

| Step | Action | Expected | Result | Screenshot |
|---|---|---|---|---|
| 1 | `./scripts/start-milestone3.ps1`, open `http://localhost:5173`, sign in as `player-a` | Signed in, profile reachable | pending | — |
| 2 | Profile → Choose picture → a PNG; reload | Thumbnail shown and survives a reload | pending | display: pending |
| 3 | Replace it with a JPEG | Old thumbnail replaced; `GET /api/users/me/picture` returns the new bytes with `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff` | pending | — |
| 4 | Sign out | Panel disappears; nothing image-related remains in the page | pending | signed out: pending |

The issue also asks for screenshots of the **missing** state (profile with no picture) and an **error** state
(for example a refused file type): pending.

## #147 cross-device journey

Script: [`tests/e2e/picture-journey.mjs`](../../tests/e2e/picture-journey.mjs), merged in
[PR #160](https://github.com/JimRashed/Web-3-Final-Project/pull/160). It has not been run on any revision.

The [post-merge review](https://github.com/JimRashed/Web-3-Final-Project/pull/160#pullrequestreview-6016902334)
found that it cannot pass on a fresh stack as merged (no profiles are created, so the picture PUT returns
404), does not cover SEC-09 sign-out across devices, does not check `picture.version`/`contentType` on B via
`GET /api/users/me`, and accepts 415 where the contract requires 400. A fix PR comes before the run below.

| Check | Expected | Result |
|---|---|---|
| `npm run journey:picture` on a fresh stack, after the fix PR | PASS output pasted with the commit it ran on | pending |
| A and B sign in separately; tokens differ | Independent sessions | pending |
| A uploads; B reads `GET /api/users/me` then the picture | B sees A's `picture.version` and `contentType`, identical bytes | pending |
| A replaces; B reads again | B sees the new version and bytes | pending |
| Rejected uploads (wrong type, mismatched magic, 2 MiB + 1) | 415, 400, 413; previous picture unchanged | pending |
| SEC-09: A signs out | A's local state and picture cleared; B's next profile and picture reads still succeed | pending (automated or manual, see below) |

**SEC-09 manual steps (only if not automated; proposed, to be confirmed by the #147 owner).** Sign in as the
same player in two separate Edge windows (one normal, one InPrivate) as A and B. On A, sign out from the app
and complete the Keycloak logout redirect. Expected: A shows the signed-out view with no profile or picture
and needs a new sign-in; B, without signing in again, reloads Profile and still shows the current picture.
Result: pending.

## Historical per-PR numbers

Reported in PR bodies at the time; **not current evidence** for `46f847b`.

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
| [#151](https://github.com/JimRashed/Web-3-Final-Project/pull/151) | Vitest |
