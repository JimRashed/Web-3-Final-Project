# Issue #34 (S02-T03) plan: Statistics API DTOs and application orchestration

Parent F02 #10, epic #7. Prerequisites #32 and #33 (merged), P02 contracts.

## Files
| Path | Change |
|---|---|
| src/QuizArena.Statistics.Api/Contracts/*.cs | `RecordResultRequest`, `StatsResponse`, `HistoryItemResponse`, `LeaderResponse` matching contracts/openapi/statistics.yaml |
| src/QuizArena.Statistics.Api/Services/IStatisticsApplication.cs, StatisticsApplication.cs | RecordResult / GetStats / GetHistory / GetLeaderboard (contract name) |
| src/QuizArena.Statistics.Api/Services/ForbiddenAccessException.cs | Self-only violation, mapped to 403 in #35 |
| src/QuizArena.Statistics.Api/Program.cs | Register the application service |
| src/QuizArena.Statistics.Data/Repositories/MatchResultRepository.cs | Review note from #81: explicit discard + comment on the conflict check |
| tests/QuizArena.Statistics.IntegrationTests/Application/StatisticsApplicationTests.cs | Mocked-repository tests and DTO-vs-contract shape checks |

## Rules
- Results are recorded only for a machine actor; the issuer comes from its validated token. Missing fields → field-level 400; `playedAt` is normalized to UTC.
- Stats/history are self-only: path userId must equal the token subject (ordinal), checked before any lookup. Paging is validated first (contract order 400 → 403).
- Leaderboard: any signed-in player, top 1..100.
- Empty stats are zeros, empty history/leaderboard are [] (repository behavior from #33).

## Validation
`dotnet build -warnaserror`, `dotnet test tests/QuizArena.Statistics.IntegrationTests`, `dotnet format --verify-no-changes`, architecture tests.
