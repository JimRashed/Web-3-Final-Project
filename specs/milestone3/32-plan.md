# Issue #32 (S02-T01) plan: Statistics domain model and pure rules

Parent F02 #10, epic #7. Prerequisite P03 #18 (merged in #72).

## Goal
A pure `MatchResult` entity plus in-memory rules for totals, leaderboard ranking, history ordering and idempotent replay, matching docs/milestone3/api-contracts.md (Statistics section).

## Files
| Path | Change |
|---|---|
| src/QuizArena.Statistics.Domain/Entities/MatchResult.cs | Immutable result keyed by (Issuer, MatchId, UserId); `Record` (score 0..5) and `Rehydrate` (historical, score >= 0) |
| src/QuizArena.Statistics.Domain/Entities/PlayerTotals.cs, LeaderboardEntry.cs | Read-model records |
| src/QuizArena.Statistics.Domain/Enums/ResultReplayDecision.cs | New / IdempotentReplay / Conflict |
| src/QuizArena.Statistics.Domain/Services/StatisticsCalculator.cs | Summarize, BuildLeaderboard, OrderHistory, DecideReplay, ShouldPersist |
| src/QuizArena.Statistics.Domain/Exceptions/*.cs | Base, InvalidMatchResult (400), ResultConflict (409) |
| tests/QuizArena.Statistics.UnitTests/Domain/*.cs | Unit tests |

## Rules
- UserId (≤ 200) and MatchId (≤ 100) are opaque strings, compared ordinally; padded values rejected. Issuer required.
- New scores 0..5; historical/imported scores only non-negative. PlayedAt is UTC (original completion time).
- Totals: every result is a game; only `Won` counts as a win, so draws are games but not wins. No results → zeros.
- Leaderboard: TotalScore desc, Wins desc, UserId ordinal asc; top 1..100 (default 10); 1-based rank.
- History: PlayedAt desc, then MatchId ordinal asc.
- Replay: same key + same score/won/playedAt → idempotent; any difference → conflict.

## Validation
`dotnet test tests/QuizArena.Statistics.UnitTests`, architecture tests, `dotnet format --verify-no-changes`.

## Out of scope
EF mapping/unique index and paging queries (#33), DTOs and page/pageSize validation (#34), endpoints (#35).
