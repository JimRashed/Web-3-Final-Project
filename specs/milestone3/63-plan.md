# Issue #63 (D02) plan: migrate legacy data safely and retire the aggregate host

Parent F07 #15, epic #7. Taken over from Jim. Implements `docs/milestone3/identity-migration.md`.

## Files
| Path | Change |
|---|---|
| scripts/migrate-legacy-data/Invoke-LegacyMigration.ps1 | Backup / DryRun / Import / Verify / Rollback; read-only on the source; ledger in QuizArenaMigrationDb |
| scripts/migrate-legacy-data/legacy-schema.sql | Legacy schema generated from the last QuizArenaContext migration (kept after the projects are removed) |
| scripts/migrate-legacy-data/Invoke-Rehearsal.ps1, rehearsal-data.sql, rehearsal-mapping.csv | Disposable end-to-end rehearsal with synthetic data and assertions |
| scripts/migrate-legacy-data/identity-mapping.example.csv | Mapping file format |
| docs/milestone3/data-cutover.md | Table rules, mapping evidence, procedure, rollback, retirement, rehearsal evidence |
| QuizArena.sln, src/QuizArena.{Api,Data,Domain}, tests/QuizArena.{UnitTests,IntegrationTests} | Aggregate host and its tests removed |
| docs/milestone3/database-operations.md, p03-service-scaffold.md | References updated |

## Acceptance mapping
- **No silent drops:** every source row gets a ledger state; Verify checks that nothing is unaccounted.
- **No coerced or invented identities:** explicit mapping file with proof; exact subjects; conflicts are rejected.
- **No password migration:** `PasswordHash` is never selected.
- **Mock matches are archived, not playable.**
- **No duplicates on rerun:** natural keys, a deterministic EventId and ledger skip.
- **Six contexts own only their schemas; no runtime reference to QuizArenaContext.**
- **Backup and rollback documented and rehearsed; deleting the source needs separate approval.**

## Validation
Rehearsal: dry run, disposable import, before/after counts, idempotent second run, rollback, source checksum. Solution build/format/tests after removing the projects; search for obsolete references.
