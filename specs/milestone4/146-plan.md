# #146 (SEC-D1) — verify Gameplay's service-to-service security contracts

> Issue: #146 · Parent: #136 (SEC-D) · Tracker: #132 · Owner: @SuSu-Lisu
> Branch: `feat/146-service-contracts`, base `feature/milestone-4-security-and-cross-device-profiles`.

## Scope

Gameplay is the only service that calls four others. This task proves that each call matches the
receiver's committed contract and carries the right machine credential — and that a failure is mapped
the way the API promises.

| Receiver | Route | Scope | Audience |
|---|---|---|---|
| Lobby | `GET /internal/lobbies/{joinCode}` | `lobbies.verify` | `quizarena-lobby` |
| QuestionBank | `POST /internal/question-sets` | `questions.play` | `quizarena-questionbank` |
| Statistics | `POST /api/results` | `results.write` | `quizarena-statistics` |
| Notification | `POST /events` | `events.write` | `quizarena-notification` |

## Method

The assertions read `contracts/openapi/*.yaml` (JSON-compatible, parsed with `JsonDocument`) rather
than duplicating the routes, so a rename on either side fails the suite. Each adapter is exercised
through a recording `HttpMessageHandler`, which lets the test assert the method, the absolute path,
the bearer token and the exact body without any network or database.

Two properties matter beyond "it returns 200":

- **The caller's token never travels.** The adapter must send the machine token only; the test asserts
  the `Authorization` header is exactly `Bearer machine-token` from the stub token source.
- **Failures stay closed.** An unknown join code is a `null` (404 to the caller), a rejected or broken
  receiver is a 503, and a Statistics 400/409 is a permanent delivery failure rather than an infinite
  retry.

## Evidence

The test class is `tests/QuizArena.Gameplay.IntegrationTests/Contracts/ServiceContractTests.cs`; CI
runs it with the rest of the backend suite. Locally the sandbox cannot host the SQL-backed suites, and
the contract file root is resolved from the repository, so CI is the recorded evidence.

## Out of scope

The cross-device picture journey (#147) and the browser-level journeys, which already exist in
`tests/e2e` from #121.
