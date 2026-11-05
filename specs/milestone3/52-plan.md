# #52 — Gameplay API DTOs and application orchestration

> Issue: #52 (S05-T03) · Feature: #13 (F05) · Epic: #7 (E00) · Owner: @SuSu-Lisu
> Depends on #50/#51 (merged) and the Lobby/QuestionBank services (merged).

## Scope of this slice

The request/response contracts and the application service that starts a match, serves the
current question and records answers. The transport adapters (typed `HttpClient` for Lobby and
QuestionBank with a machine token) and the outbox/redrive worker are the next slice — this PR
defines the ports they implement.

## Contracts

| Type | Shape |
|---|---|
| `CreateMatchRequest` | `{ joinCode }` — unknown members rejected |
| `MatchResponse` | `{ matchId, lobbyId, playerAId, playerBId, status, playerAScore, playerBScore, currentQuestionIndex, winnerUserId, draw, completedAt }` |
| `CurrentQuestionResponse` | `{ questionId, text, options }` — never carries the correct answer |
| `SubmitAnswerRequest` | `{ questionId, answer }` — unknown members rejected |
| `AnswerOutcomeResponse` | `{ correct, playerAScore, playerBScore, roundComplete, finished }` |

## Ports

- `ILobbyGateway` — returns `LobbyState(lobbyId, hostSubject, members, isLocked)` for a join code
- `IQuestionCatalog` — returns exactly five immutable `QuestionSnapshot`s

`GameplayRequestException(statusCode, message)` carries the HTTP decision (404 unknown lobby,
403 not the host, 409 lobby not locked or not exactly two members) so the domain stays free of
transport concerns.

## Orchestration rules

**Create**
1. Unknown join code → 404
2. A lobby already owns a match → return it, `Created = false` (idempotent repeated start)
3. Lobby not locked → 409
4. Caller is not the host → 403
5. Not exactly two members → 409
6. Otherwise five snapshots, `Match.Create` with a server-generated id, persist, `Created = true`

**Read** — 404 unknown, 403 non-participant. Reading never advances the match; a finished match
answers 409 for the current question.

**Answer** — the domain decides correctness, round completion and the finished transition; the
application only loads, delegates, persists and maps.

## Validation

```powershell
dotnet build QuizArena.sln
dotnet test tests/QuizArena.Gameplay.IntegrationTests --filter GameplayApplicationTests
```

Eight application tests with in-memory ports: creation, idempotent repeat, 404/409/403 paths,
non-participant read, scoring and the "no correct answer in the question response" rule.

## Next slice

Typed HTTP adapters for the two ports with a machine token, then the result/lifecycle delivery
with persisted pending state, retry/backoff and a restart-safe worker.
