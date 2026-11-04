# #50 — Gameplay domain models and pure business rules

> Issue: #50 (S05-T01) · Feature: #13 (F05) · Epic: #7 (E00) · Owner: @SuSu-Lisu
> Contract of record: `contracts/openapi/gameplay.yaml` (P02, proposed).

## Scope

Pure in-memory match rules only. Persistence (#51), API contracts (#52), endpoints and
policies (#53), React views (#54) and the coverage audit (#55) are separate issues.

## Aggregates

**`Match`** — the consistency boundary for one persisted two-player match.
- `MatchId`, `LobbyId`, `PlayerAId`, `PlayerBId`
- `Status` (`Active` / `Finished`), `PlayerAScore`, `PlayerBScore`
- `CurrentQuestionIndex` (0–4), `WinnerUserId`, `Draw`, `CreatedAt`, `CompletedAt`
- `Questions`: five immutable `QuestionSnapshot` copies
- `Answers`: append-only `RoundAnswer` records

**`QuestionSnapshot`** — question id, text, four distinct options and the correct answer.
The correct answer is never exposed by the API layer.

**`AnswerOutcome`** — `Correct`, both scores, `RoundComplete`, `Finished`.

## Rules implemented

1. A match requires exactly five distinct question snapshots and two distinct players.
2. Only members may answer; anyone else gets `MatchAccessDeniedException` (→ HTTP 403).
3. The submitted question id must equal the current round's question
   (otherwise `MatchConflictException` with `StaleQuestion` → HTTP 409).
4. One answer per player per round; a duplicate raises `DuplicateAnswer` (→ HTTP 409).
5. The match advances only when both players have answered the round.
6. A correct answer adds exactly one point to that player.
7. After round five the match becomes `Finished`: higher score wins, equal scores produce a
   draw with no winner, and `CompletedAt` is set.
8. A finished match is immutable; further answers raise `MatchFinished` (→ HTTP 409).
9. Reading the current question never mutates state.

## Deliverables

- `src/QuizArena.Gameplay.Domain/Entities/{Match,QuestionSnapshot,AnswerOutcome,RoundAnswer}.cs`
- `src/QuizArena.Gameplay.Domain/Enums/MatchStatus.cs`
- `src/QuizArena.Gameplay.Domain/Exceptions/{MatchDomainException,MatchAccessDeniedException,MatchConflictException,InvalidMatchException}.cs`
- `tests/QuizArena.Gameplay.UnitTests/MatchTests.cs`

## Validation

```powershell
dotnet build tests/QuizArena.Gameplay.UnitTests/QuizArena.Gameplay.UnitTests.csproj
dotnet test  tests/QuizArena.Gameplay.UnitTests/QuizArena.Gameplay.UnitTests.csproj
```

Domain project keeps zero package, framework and project references.
