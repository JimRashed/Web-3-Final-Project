# Issue #35 (S02-T04) plan: Statistics REST endpoints with OpenAPI

Parent F02 #10, epic #7. Prerequisites #34 (application layer, merged in #85) and P06 #21 (policies, merged in #73).

## Files
| Path | Change |
|---|---|
| src/QuizArena.Statistics.Api/Controllers/StatisticsController.cs | POST /api/results, GET stats / matches / leaderboard |
| src/QuizArena.Statistics.Api/Services/StatisticsExceptionHandler.cs | 400 field errors, 403 self-only, 409 conflicting result; aborted requests not 500 |
| src/QuizArena.Statistics.Api/Contracts/ExplicitOffsetDateTimeConverter.cs, RecordResultRequest.cs | `playedAt` must carry an explicit offset |
| src/QuizArena.Statistics.Api/Program.cs, *.csproj | Unknown JSON members rejected, exception handler, Development-only OpenAPI |
| tests/QuizArena.Statistics.IntegrationTests/Endpoints/*.cs | Real-pipeline HTTP tests; leaderboard in its own class/database |

## Behavior (contracts/openapi/statistics.yaml)
- POST /api/results: `results.write` machine only; 200 empty for new and identical retries; 409 on different content; 400 for missing/invalid/unknown fields or a timestamp without offset.
- GET /api/players/{userId}/stats and /matches: `stats.read`, self only (ordinal) → 403 otherwise; history paging 1..100 → 400 first.
- GET /api/leaderboard: `leaderboard.read`, any player; top 1..100, default 10.
- `/openapi/v1.json` only in Development, matching the contract operations.

## Validation
`dotnet build -warnaserror`, `dotnet test tests/QuizArena.Statistics.IntegrationTests`, `dotnet format --verify-no-changes`, architecture tests.
