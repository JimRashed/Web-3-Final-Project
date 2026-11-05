# #55 — Gameplay vertical-slice, authorization and regression coverage

> Issue: #55 (S05-T06) · Feature: #13 (F05) · Epic: #7 (E00) · Owner: @SuSu-Lisu
> Base: `feature/milestone-3-layered-service-development`. PR: #117.

## Research — what the slice already had

Every layer task (#50–#54) landed with its own tests, so this task is an audit, not a redesign:

| Layer | Existing evidence |
|---|---|
| Domain | `MatchTests`: both players, wrong/current answer, duplicate, outsider, draw, round advance, immutability |
| Application | `GameplayApplicationTests`: create, idempotent repeat, 404/409/403 paths, non-participant read, scoring, no answer key in the question DTO |
| Relational | `MatchRepositoryTests`, `GameplayRaceTests`: concurrency, atomic completion, no lost answer |
| Outbound | `DeliveryRetryPolicyTests`, `DeliveryChannelsTests`, `DeliveryWorkerTests`, `MatchDeliveriesTests` |
| Adapters | `LobbyHttpGatewayTests`, `QuestionBankHttpClientTests` |
| UI | `MatchPage.test.tsx` through #100/#109 |

## The two gaps this task closes

1. **The shipped routes had no HTTP-level tests.** `SecurityMatrixTests` exercises a
   `__security-probe` controller; nothing called `POST /matches`, `GET /matches/{id}` or
   `POST /matches/{id}/answers` through the real pipeline.
2. **No test proved the outbox commit is atomic with the match.** The planner and the worker were
   covered; the "match and its four rows in one `SaveChanges`" claim was not.

## Implementation

- `tests/.../Endpoints/GameplayApiFactory.cs` — real pipeline, in-memory `IMatchRepository`, and the
  two remote services replaced at their port boundary (the seam the HTTP adapters implement).
- `tests/.../Endpoints/MatchesEndpointTests.cs` — 19 cases: 401/403 scope, 201 + `Location`,
  idempotent 200, 404/403/409/409/503 create failures that persist nothing, participant reads,
  public-only question DTO, 400 invalid option, 409 duplicate, 403 non-participant, 200 outcome.
- `tests/.../Persistence/MatchRepositoryTests.cs` — two relational tests: a finished match commits
  with four `Pending` rows (2 results + 2 events, unique idempotency keys); creation commits its two
  `MATCH_CREATED` rows.

No production code changes.

## Validation

`dotnet build QuizArena.sln` clean; the full backend suite (including these tests on the SQL Server
service container) and the frontend suite are green on #117 in CI. The cross-service restart/retry
demonstration is #64.

## Human gates

- **Gate 1** — this plan, reviewed before implementation.
- **Gate 2** — diff inspected; the run exposed a real defect in the new tests (write-scoped token on a
  read route → 403) that was fixed in `8fcf796` rather than waived; CI re-run green.
- **Gate 3** — CI evidence on PR #117; independent peer signoff requested from @JimRashed.
