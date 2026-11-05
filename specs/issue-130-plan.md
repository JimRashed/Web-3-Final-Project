# Issue #130 — live two-player demo

[Issue #130](https://github.com/JimRashed/Web-3-Final-Project/issues/130) follows the integration work in #62 and #64. Starting milestone revision: `422dc74`. This is an agent-reviewed plan for verifying the current demo from a fresh setup and repairing only blockers observed along that path.

## Summary and architecture

Use the React browser clients, real Keycloak user sessions, and the six live APIs with fresh service databases. Gameplay must retrieve persisted snapshots through QuestionBank's secured internal endpoint. Keep bearer policies, service boundaries, SQL ownership, and public answer secrecy intact. Statistics and notification delivery should use the existing service integration.

## File inventory

| Path | Change |
| --- | --- |
| `specs/issue-130-plan.md` | This scoped plan. |
| `docs/milestone3/runbook.md` | Document the verified setup, provisioning, migrations, seed validation, startup and two-player rehearsal. Preserve Docker instructions and add a portable Windows provider option if verified. |
| `docs/milestone3/validation-results.md` | Append actual #130 evidence and limitations while preserving prior records. |
| `src/QuizArena.Client/src/auth/session.ts` | Preserve only the shared match ID across the existing OIDC callback in tab-scoped storage. |
| `src/QuizArena.Client/src/main.tsx` | Restore the local match destination on a successful callback before mounting the router. |
| `src/QuizArena.Client/src/auth/session.test.ts` | Verify shared-link sign-in and one-time restoration without changing the registered callback. |

The shared-link rehearsal exposed a blocker: opening the URL reloads the client, whose in-memory session needs sign-in. The callback then sends the player to the overview and loses the match ID. Keep the registered callback and all authorization checks. Carry only the match ID and build a fixed local match path after authentication.

## Ordered steps

1. Provision a fresh local realm and a unique LocalDB instance. Keep generated development credentials in ignored configuration. Use Keycloak 26.7.5 and Java 21 for the portable setup, import the checked-in realm, and verify discovery at port 8080. Keep the provider bound to loopback. Provision `player-a` and `player-b` via the existing Node script. Run the documented migrations and launch all six APIs and React. Record exact commands, prerequisites and health results.
2. Query the migrated QuestionBank catalog in the isolated instance. Record the count of questions with four distinct nonblank options and an answer that matches an option. Expect at least five. Capture the successful secured internal question-set request during match creation and verify played IDs against persisted IDs without exposing answers or tokens.
3. Run the applicable #64 automated journeys and focused solution/frontend checks below. Record the revision, commands, exit status, counts and any failures. Treat previous milestone results as historical.
4. Rehearse in two independent browser sessions. Sign in separately, create and join one lobby, lock it as host and start one match. Open the same match as the guest through the shared match link to exercise the notification-delay fallback. Answer exactly once per player per round, verify both views advance through all five rounds, and compare final scores and outcome. Verify expected notifications and persisted results/statistics for both players. Record redacted observations with the exact tested revision.
5. If a blocker occurs, reproduce it, make the smallest in-scope fix and rerun affected checks and the full rehearsal. If a missing capability needs broader work, report precise repro and follow-up scope to Dispatcher and keep #130 open. Append measured results and submit the scoped diff for read-only review before draft publication.

## Acceptance coverage

| Criterion | Evidence |
| --- | --- |
| Fresh setup, migrations, provisioning and accounts without credentials | Step 1 and updated runbook |
| At least five valid persisted questions and secured internal fetch | Step 2 aggregate SQL and request evidence |
| Host create, join, lock and start one match | Step 4 two-session rehearsal |
| Same match, five rounds and consistent score/outcome | Step 4 matching observations for both players |
| Notifications, statistics and fallback | Step 4 shared link and delivered/persisted outcomes |
| Automated and separate manual evidence with revision | Steps 3–4 and validation report |
| Focused repair or linked broader follow-up | Step 5, including rerun or remaining blocker |

## Testing and validation commands

Run from the issue worktree. With the portable provider already running, provision users separately because `-SkipKeycloak` also skips the provisioning branch:

```powershell
sqllocaldb create QuizArenaDemo130 -s
$env:QUIZARENA_SQLSERVER='Server=(localdb)\QuizArenaDemo130;Trusted_Connection=True;TrustServerCertificate=True'
node --env-file=.env infra/oidc/provision-demo-users.mjs
./scripts/start-milestone3.ps1 -SkipKeycloak
```

Reuse the existing live journeys against the isolated stack, supplying local credentials through the environment as documented. The retry journey temporarily disrupts downstream delivery, so run it only against the isolated stack and separately from the manual session.

```powershell
Push-Location tests/e2e
npm ci --no-audit --no-fund
npm run journey:api
npm run journey:ui
# Run journey:retry when verifying an affected retry/delivery path.
Pop-Location
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal
```

For frontend changes run client `npm ci --no-audit --no-fund`, `npm run typecheck`, `npm run lint`, `npm run test -- --run`, and `npm run build`. Record exact commands actually used, including any offline restore source. A test or health probe cannot substitute for the separate manual rehearsal.

## Risks and exclusions

The provider, machine client, isolated SQL and separate browser sessions must all be available. Preserve existing user data and processes. Do not print or commit secrets, raw tokens, environment files, answer keys, generated dependencies or sensitive logs. Feature expansion and broad retry-system redesign are outside scope. Pending environment requirements, incomplete acceptance and human review must remain explicit.
