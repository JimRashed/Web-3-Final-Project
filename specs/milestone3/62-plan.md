# Issue #62 (D01) plan: run the six services and the client from a documented local environment

Parent F07 #15, epic #7. Taken over from Jim. Prerequisites (P04, P05, P07 and the service endpoints) are merged.

## Files
| Path | Change |
|---|---|
| scripts/start-milestone3.ps1 | Checks prerequisites (SDK 10, LocalDB or `QUIZARENA_SQLSERVER`, Docker, Node, `.env` keys without printing values); starts Keycloak and waits for readiness; optionally provisions the demo players; builds; applies migrations only; starts the six APIs (Development, own port) and the client; waits for and prints health. Records pids in `.run/pids.json`. |
| scripts/stop-milestone3.ps1 | Stops the recorded process trees; `-Keycloak` runs `docker compose stop`. Never deletes volumes or databases. |
| docs/milestone3/runbook.md | Prerequisites with versions, start/stop, ports and health URLs, configuration overrides, troubleshooting, explicit manual reset, validation record |
| .gitignore | `.run/` |

## Acceptance mapping
- **Every /health reachable and the client renders:** shown in the status table and the validation record.
- **Services start independently:** `-Services Identity` starts only that host.
- **Prerequisites and troubleshooting explicit:** see the runbook.
- **Restart preserves data:** row counts recorded before and after a full stop/start including Keycloak.
- **No destructive reset:** the scripts only run `migrations update` and `compose up`/`stop`.
- **Configuration through the environment:** documented overrides; the secret stays only in `.env`.

## Validation
Run start → stop → stop with Keycloak → start → single-service start, and compare data counts. A teammate follows the runbook from a clean checkout (needed before the issue closes).
