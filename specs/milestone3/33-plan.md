# Issue #33 (S02-T02) plan: Statistics EF mapping, migration and repository

Parent F02 #10, epic #7. Prerequisites #32 (MatchResult, merged in #77) and P04 #19 (contexts, merged in #78).

## Files
| Path | Change |
|---|---|
| src/QuizArena.Statistics.Domain/Repositories/IMatchResultRepository.cs | Persistence port |
| src/QuizArena.Statistics.Domain/Services/StatisticsCalculator.cs | `ValidatePaging` / `ValidateTop` (page ≥ 1, size 1..100, top 1..100) |
| src/QuizArena.Statistics.Data/Configurations/MatchResultConfiguration.cs, UtcConverter.cs | `MatchResults` table, surrogate identity key, unique (Issuer, MatchId, UserId), history index, score check constraint, binary collation, UTC |
| src/QuizArena.Statistics.Data/Repositories/MatchResultRepository.cs | Find, idempotent Record, SQL totals / history page / leaderboard |
| src/QuizArena.Statistics.Data/Context/StatisticsDbContext.cs, StatisticsPersistenceExtensions.cs | DbSet and scoped registration |
| src/QuizArena.Statistics.Data/Migrations/*_AddMatchResults.cs | Migration |
| tests/QuizArena.Statistics.UnitTests/Domain/PagingRulesTests.cs | Paging rule tests |
| tests/QuizArena.Statistics.IntegrationTests/Persistence/MatchResultRepositoryTests.cs | Relational tests on a per-test SQL Server database |

## Decisions
- Binary collation on Issuer, MatchId and UserId so keys compare and ORDER BY sorts ordinally, exactly like `StatisticsCalculator`.
- Recording uses `StatisticsCalculator.ShouldPersist` before and after a duplicate-key race, so concurrent deliveries resolve to one row (identical) or a conflict (different).
- Aggregation runs in SQL; tests compare it against the pure calculator on the same data.
- Historical scores > 5 are allowed in storage (legacy import); a check constraint still rejects negative scores.

## Validation
`dotnet test` for Statistics unit + integration projects (3 repeat runs of the persistence tests), mutation checks for race recovery and collation, `dotnet build -warnaserror`, `dotnet format --verify-no-changes`.
