# #51 — Gameplay EF mappings, migrations and repositories

> Issue: #51 (S05-T02) · Feature: #13 (F05) · Epic: #7 (E00) · Owner: @SuSu-Lisu
> Depends on #50 (domain, merged in #75) and #19 (service-owned databases, merged in #78).

## Scope

Persist the **`Match` aggregate** in the Gameplay service's own database. No API, controller or
client work (#52–#55).

## Mapping decision

`Match` is a consistency boundary: its five question snapshots and its answers are only ever
read together with the match, and the aggregate enforces every transition. The mapping therefore
keeps **one row per match** and stores the two immutable child collections as canonical JSON
documents:

| Table | Columns |
|---|---|
| `Matches` | `MatchId` (PK, nvarchar(100), binary collation) · `LobbyId` · `PlayerAId` · `PlayerBId` · `Status` · `PlayerAScore` · `PlayerBScore` · `CurrentQuestionIndex` · `WinnerUserId` · `Draw` · `CreatedAt` · `CompletedAt` · `QuestionsJson` · `AnswersJson` |

Indexes:
- `UX_Matches_LobbyId` (unique) — one match per lobby, which is what prevents a duplicate start.
- `IX_Matches_PlayerA_CreatedAt` and `IX_Matches_PlayerB_CreatedAt` — "my matches", newest first.

The persistence row is a separate `MatchRecord` in `Data`; the repository maps it to and from the
domain aggregate, so the domain keeps its private constructor and its invariants.

## Domain addition

`Match.Rehydrate(...)` rebuilds a stored match — including a finished one — without replaying
round transitions. It re-validates the shape (five snapshots, index range, score range, completion
time present when finished, UTC timestamps). Same pattern as `PlayerProfile.Rehydrate` in #26.

## Repository behaviour

- `FindAsync` reads with `AsNoTracking()` and rebuilds the aggregate.
- `SaveAsync` inserts or replaces the stored state; a lost insert race is recovered by catching
  the duplicate-key error (2601/2627), reloading and applying the update on top.

## Validation

```powershell
dotnet build QuizArena.sln
dotnet test  tests/QuizArena.Gameplay.IntegrationTests
```

Relational tests create a uniquely named database per test, apply the migrations and drop it
afterwards (`QUIZARENA_TEST_SQLSERVER` overrides the LocalDB default).

## Out of scope

Answer-history queries, cross-service adapters to Lobby/QuestionBank/Statistics, and the
outbox delivery worker — all belong to #52–#53 and the delivery task.
