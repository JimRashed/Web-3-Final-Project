# Legacy data cutover (D02)

How to move owned data from the Milestone 2 `QuizArenaDb` into the six service databases, verify it and roll it back. Issue [#63](https://github.com/JimRashed/Web-3-Final-Project/issues/63). It implements the transfer rules in [identity migration](identity-migration.md) and the constraints in [database operations](database-operations.md#legacy-constraints-the-data-tasks-must-preserve).

The tool is `scripts/migrate-legacy-data/Invoke-LegacyMigration.ps1`. It only ever **reads** the source, and it records every source row in a separate ledger database (`QuizArenaMigrationDb`), so no service schema gains migration columns.

## What happens to each legacy table

| Legacy table | Destination | Rule |
|---|---|---|
| `Users` | Identity `UserProfiles` | Only with a **verified mapping** (legacy user id → issuer + subject). Display name = username (trimmed). `PasswordHash` is never read; Keycloak owns credentials. If the player already has a profile in the new service, it is kept as is (`AlreadyPresent`). |
| `MatchResults` | Statistics `MatchResults` | Player via the mapping. GUID match ids become lowercase canonical text. Historical scores above 5 are kept, and negative scores are quarantined. An identical existing result is `AlreadyPresent`, and a different one is quarantined (never overwritten). |
| `UserPrefs` | Notification `UserPreferences` | The player is referenced by GUID text or exact username. Preferences the player already set in the new service win; the legacy value is quarantined. |
| `EventLogs` | Notification `Events` | `MATCH_CREATED`/`MATCH_COMPLETED` with a `matchId` payload. The winner is mapped as well. The `EventId` is deterministic (an MD5 of the source database and legacy id), so reruns never duplicate it. Imported events are history only and are never re-sent. The legacy app counted a tie as a win for player A; the winner is imported as recorded. |
| `Questions`, `Categories` | — (quarantined) | Legacy questions have **no answer options**, and the QuestionBank requires exactly four. They are kept in the ledger, not completed by guessing. |
| `ActiveLobbies`, `LobbyPlayers`, `ActiveMatches`, `PlayerStates` | — (archived) | Transient state from mock-question matches is archived, never resumed as lobbies or playable five-round matches (ADR-08). |

Every source row ends in exactly one state: `Imported`, `AlreadyPresent`, `Quarantined` (with a reason) or `Archived`. The full source row (minus the password hash) is stored as JSON in the ledger, so nothing is silently dropped.

## Identity mapping file

A CSV supplied by the operator, using the format of `scripts/migrate-legacy-data/identity-mapping.example.csv`:

```
LegacyUserId,Issuer,Subject,VerifiedBy,ProofReference
```

Each entry needs **independent proof** that the Keycloak account belongs to that legacy user, for example the player signing in and confirming their old username, or a support-verified recovery. A matching username or email alone is not proof. Rows are rejected (and the user's data quarantined) when:
- the id is not a GUID or isn't in the source;
- a field is missing;
- the subject is padded or too long;
- a legacy user appears twice;
- two legacy users map to one subject (merging needs explicit approval).

Subjects are used exactly as given and are never lower-cased or trimmed. The file's SHA-256 is recorded with the run.

## Procedure

Run from the repository root. The source and target databases must be on the same SQL Server instance (the tool uses cross-database queries). Add `-Server` and `-ConnectionOptions` for an instance other than LocalDB.

1. **Freeze** legacy writes (stop the old host) and apply the service migrations: `./scripts/start-milestone3.ps1 -SkipFrontend`, or the commands in [database operations](database-operations.md#commands).
2. **Back up:** `./scripts/migrate-legacy-data/Invoke-LegacyMigration.ps1 -Mode Backup -BackupDirectory D:\backups` writes a COPY_ONLY backup with CHECKSUM and runs `RESTORE VERIFYONLY`.
3. **Dry run:** `-Mode DryRun -MappingFile .\identity-mapping.csv` prints the rejected mapping rows and, per table, how many rows would be imported, already present, quarantined or archived, with reasons. **It writes nothing.** Resolve mapping problems and rerun until the plan is what the owners expect.
4. **Import:** `-Mode Import -MappingFile .\identity-mapping.csv` applies the plan in **one transaction** and prints the run id. Rerunning is safe: rows a previous run imported are left as they are, and nothing is duplicated. A later run can import rows that were quarantined only because their mapping was missing.
5. **Verify:** `-Mode Verify` checks:
   - for every legacy table, *source rows = imported + already present + quarantined + archived (+ rolled back)*, with nothing unaccounted;
   - every imported row exists in its target;
   - each mapped player's games, wins and total score match between the source and Statistics.

   It exits non-zero on any mismatch.
6. Owners review the ledger (`QuizArenaMigrationDb.dbo.Ledger`, `IdentityMapping`, `Runs`), then switch the client and APIs over.

## Rollback

- **Before the new services accept writes:** `-Mode Rollback -RunId <id>` removes exactly the rows that run imported and marks them `RolledBack`. It only removes a row that is **unchanged since import**: a profile that was renamed, or preferences the player changed afterwards, are kept and reported. The source was never modified, so routing back to the old host is enough.
- **After the new services have accepted writes:** do not route back to the stale legacy data. Freeze writes, snapshot the service databases and reconcile forward, as in [identity migration](identity-migration.md#ordered-transfer-and-reconciliation).
- **Deleting the source database or the backup** is outside this tool and needs separate, explicit approval from the owners.

## Retired aggregate host

`src/QuizArena.{Api,Data,Domain}` and `tests/QuizArena.{UnitTests,IntegrationTests}` were removed from the solution and the repository. The legacy schema the tool reads is preserved in `scripts/migrate-legacy-data/legacy-schema.sql` (generated from the last legacy migration), and the Milestone 2 docs stay in `docs/` marked as historical. No runtime code references `QuizArenaContext`. The six service contexts each own only their schema, and the architecture tests still reject any reference to the old projects.

## Rehearsal evidence

`./scripts/migrate-legacy-data/Invoke-Rehearsal.ps1` builds a disposable `Rehearsal_QuizArenaDb` from `legacy-schema.sql` plus synthetic data (`rehearsal-data.sql`, all identities invented) and `Rehearsal_` copies of the target databases from their own migrations. It then runs every mode and asserts the outcome. Run on 2026-11-04 on LocalDB 17.0: **34/34 checks passed**, including:

| Step | Checked |
|---|---|
| Backup | file written and verified |
| Dry run | nothing written |
| Import | 1 profile imported, 1 existing live profile kept unchanged, 1 unmapped user quarantined; 3 results (including a historical 7) with lowercase match ids, 1 quarantined; preferences resolved by GUID and by username; created/completed events imported with the winner mapped, an unknown type and a non-JSON payload quarantined; questions quarantined (no options); 6 lobby and match rows archived; 2 bad mapping rows rejected; no password hash anywhere in the ledger |
| Verify | nothing unaccounted, imported rows present, per-player totals equal |
| Second import | no duplicates in any target |
| Rollback | imported rows removed, a preference the player changed after import kept, the pre-existing profile kept, verify still passes |
| Source | checksum identical before and after |

The rehearsal also caught three defects in the tool before any real use: temp tables lost inside parameterised batches, collation conflicts between tempdb and the binary-collated service columns, and the same conflict in rollback. All three were fixed and rerun.
