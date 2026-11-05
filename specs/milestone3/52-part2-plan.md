# #52 part 2 — Typed service adapters and the durable delivery outbox

> Issue: #52 (S05-T03) · Feature: #13 (F05) · Epic: #7 (E00) · Owner: @SuSu-Lisu
> Base: `feature/m3/53-gameplay-endpoints` (stacked on #95/#96). PR target: the milestone branch.

## Scope of this slice

Part 1 (#95) defined the ports and the orchestration; this slice replaces the two fail-closed
placeholders with real HTTP adapters and adds the delivery path the orchestration commits into:

1. a Keycloak client-credentials token client for the Gameplay machine client;
2. a typed adapter for Lobby (`GET /internal/lobbies/{joinCode}`, scope `lobbies.verify`);
3. a typed adapter for QuestionBank (`POST /internal/question-sets` `{ count }`, scope `questions.play`);
4. a transactional outbox that stores every result/lifecycle message with the match, and a
   restart-safe worker that delivers it with bounded retries.

## Wire contracts consumed

| Receiver | Route | Scope (machine) | Body / response |
|---|---|---|---|
| Lobby | `GET /internal/lobbies/{joinCode}` | `lobbies.verify` | `{ lobbyId, joinCode, status, hostUserId, playerIds }`; 404 = unknown code |
| QuestionBank | `POST /internal/question-sets` | `questions.play` | `{ count: 5 }` → five `{ id, prompt, category, options, correctAnswer }`; 409 = catalog too small |
| Statistics | `POST /api/results` | `results.write` | `{ matchId, userId, score, won, playedAt }` |
| Notification | `POST /events` | `events.write` | `{ eventId, type, userId, payload: { matchId, winnerUserId?, draw? } }` |

The realm maps each scope to the consuming service's audience, so a token is valid for exactly one
receiver. Audience/issuer validation therefore stays exactly as every other service has it.

## Machine token client

`KeycloakMachineTokenSource` posts `grant_type=client_credentials` to
`{Authentication:Authority}/protocol/openid-connect/token` with `Authentication:MachineClientId` and
`Authentication:MachineClientSecret` (environment/user-secrets only; the committed value is empty).
Tokens are cached per scope until 30 seconds before expiry, and a receiver's 401 invalidates the
cached token and retries once before the failure is treated as a configuration fault.

## Outbox

`Deliveries` (Gameplay's own database, same transaction as `Matches`):

| Column | Purpose |
|---|---|
| `Id`, `Destination`, `Operation` | row identity, receiver and wire operation/version |
| `PayloadJson`, `IdempotencyKey` | frozen body and the receiver-facing dedupe key |
| `Status` (`Pending`/`Delivered`/`Failed`), `AttemptCount`, `NextAttemptAt`, `LeaseUntil` | scheduling state |
| `LastError`, `CreatedAt`, `DeliveredAt` | safe diagnostics and evidence |

- **Enqueue.** Creation commits two `MATCH_CREATED` events; the final answer commits two results and
  two `MATCH_COMPLETED` events in the same `SaveChanges` as the finished match. A unique
  `(Destination, IdempotencyKey)` index makes a replayed enqueue a no-op.
- **Claim.** `SELECT … WITH (UPDLOCK, ROWLOCK, READPAST)` inside a short transaction, then a lease of
  30 seconds. A crashed worker's lease expires and the row is claimable again with its attempt count
  and payload untouched — restart recovery needs no special case.
- **Backoff.** Eight attempts total, `min(300 s, 2^(attempt-1))` plus up to one second of jitter, and
  a receiver `Retry-After` honoured up to 300 seconds. Only network failures, timeouts, 408, 429 and
  5xx are retried; other 4xx are permanent immediately.
- **Terminal states.** Exhausted or permanently rejected rows become `Failed` and stay visible; a
  second 401 from a freshly minted token is a configuration failure, not a retry loop.
- **Redrive.** `dotnet run -- --redrive <deliveryId>` requeues a failed row with a fresh attempt
  series. No admin HTTP endpoint is added (matches the milestone decision).

## Files

| File | Change |
|---|---|
| `src/QuizArena.Gameplay.Api/Services/GameplayServiceOptions.cs` | `ServiceUrls` + per-call timeout + scope names. |
| `src/QuizArena.Gameplay.Api/Services/MachineTokens.cs` | Keycloak client-credentials client, cache and invalidation. |
| `src/QuizArena.Gameplay.Api/Services/ServiceClients.cs` | `LobbyHttpGateway`, `QuestionBankHttpClient`; non-2xx fails closed as 503. |
| `src/QuizArena.Gameplay.Api/Services/MatchDeliveries.cs` | Builds the frozen creation/completion payloads. |
| `src/QuizArena.Gameplay.Api/Services/Outbox/*` | Attempt, retry policy, HTTP channels, worker. |
| `src/QuizArena.Gameplay.Domain/Repositories/MatchDelivery.cs` | Domain-level delivery descriptor (no EF, no HTTP). |
| `src/QuizArena.Gameplay.Domain/Repositories/IMatchRepository.cs` | `SaveAsync(match, deliveries, ct)` for the atomic commit. |
| `src/QuizArena.Gameplay.Data/…` | `DeliveryRecord`, configuration, `OutboxStore`, migration `AddDeliveries`. |
| `src/QuizArena.Gameplay.Api/Program.cs` | Registration, worker, `--redrive`, placeholder removal. |
| `tests/…` | Retry policy, channels, both adapters, payload planner, worker decisions. |

`UnavailablePorts.cs` is deleted: the 503 placeholders are now unreachable code.

## Validation

```powershell
dotnet build QuizArena.sln --no-restore -m:1
.tools/TestRunner/bin/Debug/net10.0/TestRunner.exe -u          # 89 passed
.tools/TestRunner/bin/Debug/net10.0/TestRunner.exe DeliveryWorkerTests   # 5 passed
.tools/TestRunner/bin/Debug/net10.0/TestRunner.exe MigrationTests        # model-vs-migration check passed
```

The SQL Server-backed integration tests (persistence, endpoints) need a database and run in CI;
this workstation has no LocalDB instance, so only the model comparison and the in-memory tests were
executed here. `MigrationTests.Model_ComparedWithLatestMigration_HasNoUnmigratedChanges` passed, so
the scaffolded migration matches the model exactly.

## Deliberately out of scope

- A broker or distributed transaction (ADR-07 keeps the row as the message).
- Public retry/admin endpoints.
- Rework of the #53 endpoints, which this branch builds on unchanged.
