# Milestone 3 validation results

These are actual results, recorded on 2026-11-04 against `feature/milestone-3-layered-service-development` at `ae896cf`. The CI workflow `.github/workflows/ci.yml` runs the same backend, architecture, security and frontend gates on every PR.

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
| QuestionBank unit / integration | 70 / 140 |
| Lobby unit / integration | 174 / 277 |
| Gameplay unit / integration | 16 / 51 |
| Notification unit / integration | 29 / 105 |
| Legacy QuizArena unit / integration (historical aggregate host) | 24 / 11 |
| **Backend total** | **1,348 passed, 0 failed, 0 skipped (15 assemblies)** |
| React client (19 files) | **225 passed** |

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

## Pending at the time of writing
| Item | Status |
|---|---|
| D03 #64: full secured two-player journey and delivery retries | not yet run. Depends on the Gameplay fixes below. Add the results here when complete. |
| Gameplay #95/#96/#100 | review found a repeated-start authorization gap, 500s on simultaneous start and answer, and an unreachable or incorrect match page. Fixes are proposed in #107, #108 and #109, each with tests that fail before the fix. |
| Notification `GET /events` | merged without the contract's paging and `userId` 403 rule (review on #90). Follow-up needed. |
| Gameplay outbox and redelivery (ADR-07) | planned next slice of #52 |
