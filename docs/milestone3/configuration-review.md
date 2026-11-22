# Configuration and secret-handling review (SEC-A2, #138)

Status: agent review evidence from the Dispatcher pipeline, recorded on 2026-11-22. It is **not** an approval of any SEC decision; the approval table in [security-decisions.md](security-decisions.md) is unchanged. The residual risks at the end are the hand-off to the threat model (#139).

Environment setup, the per-service key matrix and rotation are in the [runbook](runbook.md#environment-configuration-sec-a2-138).

## What was reviewed

- Problem bodies and exception handlers of the six hosts (`src/QuizArena.<Service>.Api/Errors/`, `Security/SecurityExtensions.cs`).
- Gameplay outbound adapters, their logs and the machine-token client (`Services/ServiceClients.cs`, `Services/MachineTokens.cs`, `Services/Outbox/`).
- Startup configuration validation (`Security/SecurityExtensions.cs`, Gameplay `Program.cs`, the six `*PersistenceExtensions.cs`).
- Local tooling: `scripts/start-milestone3.ps1`, `infra/compose.yaml`, `.env.example`, `src/QuizArena.Client/.env.example`.
- CI: `.github/workflows/ci.yml`.
- Client token handling: `src/QuizArena.Client/src/auth/session.ts`, `src/api/httpClient.ts`.

## Findings

| # | Finding | Evidence | Outcome |
|---|---|---|---|
| F1 | CI committed a SQL Server SA password and a connection string containing it. | `ci.yml` (before #138): `services.sqlserver.env.MSSQL_SA_PASSWORD` and job env `QUIZARENA_TEST_SQLSERVER` | Fixed: the `Start SQL Server` step generates a per-job password, masks it before use and exports the connection string through `GITHUB_ENV`. No literal remains. |
| F2 | Startup accepted any absolute URI scheme for the Authority, plain-http metadata and an empty CORS list outside Development. | `SecurityExtensions.cs` options chain (all six hosts) | Fixed: http/https Authority everywhere; https Authority, `RequireHttpsMetadata=true` and valid CORS origins outside Development. |
| F3 | Gameplay accepted blank `ServiceUrls:*` and a blank machine secret at startup; failures surfaced only per call (503) or as outbox retries. | Gameplay `Program.cs`, `ServiceClients.cs` `Endpoint`, `MachineTokens.cs` configuration check | Fixed: `ValidateOnStart` for the four URLs (all environments) and the secret (outside Development). The lazy checks stay as defence in depth. |
| F4 | All startup messages name the key only. | `StartupConfigurationTests` (Gameplay, Identity) assert the key is present and a unique sentinel value is absent | No leak. |
| F5 | The machine-token client never surfaces the identity provider's body; its exception names only the HTTP status. | `MachineTokens.cs:100-102` | Verified by `MachineTokenLeakTests` in Development and Production: the 503 body holds neither the secret nor the IdP body, and no `exception`/`stackTrace` member. |
| F6 | Exception handlers pass the exception to `ProblemDetailsContext`, but the default writer adds no exception detail in either environment. | `GameplayExceptionHandler.cs:42`, `LobbyExceptionHandler.cs:36`, `NotificationExceptionHandler.cs:37`, `QuestionBankExceptionHandler.cs:27` | No change needed (F5 test covers Gameplay under Development). |
| F7 | Adapter error logs include the exception for transport failures, which can contain the internal host and port. | `ServiceClients.cs:61`, `:147`; `DeliveryChannels.cs:124` | Residual risk R2. No secret is logged; the `Authorization` header is never logged. |
| F8 | Missing or blank connection strings fail at startup with a key-only message. | `*PersistenceExtensions.cs` (e.g. `IdentityPersistenceExtensions.cs:16`) | Covered by `StartupConfigurationTests`. Malformed values: residual risk R4. |
| F9 | The start script checks `.env` keys without printing values, forces Development and passes the Gameplay secret only to Gameplay. | `start-milestone3.ps1:63-69`, `:120`, `:123-125` | No change. |
| F10 | Compose fails fast on missing Keycloak admin and Gameplay secret variables. | `infra/compose.yaml:12-15` | No change. |
| F11 | The client keeps tokens in memory, sends them only in the `Authorization` header (`credentials: 'omit'`, `redirect: 'error'`), stores only the match ID across sign-in, and builds match links with `matchId` only. | `session.ts:85-98`, `httpClient.test.ts:14-15` | New tests: `session.test.ts` (sessionStorage holds only the match ID; callback destination has only `matchId`; snapshots carry no token), `LobbyPage.test.tsx` (match route has only `matchId`). |
| F12 | The client `.env.example` holds only public `VITE_*` settings. | `src/QuizArena.Client/.env.example` | New test in `config.test.ts`. |
| F13 | The profile response has no token-like field. | `ProfileEndpointsTests.PutMe_ThenGetMe_ReturnsProfileForTokenIdentity` asserts the exact property set `id, issuer, subject, displayName` | Already covered. Picture endpoints are not implemented yet; SEC-03/SEC-08 bind #144/#145 to Blob/object URLs with no token or storage URL in `src`. |

## Residual risks for #139

- **R1** Unhandled exceptions are logged in full. SQL exceptions can show the SQL host, database and user (never the password).
- **R2** Adapter logs may show internal host:port of the downstream services.
- **R3** Raising `Debug`/`Trace` logging for authentication, EF Core or `System.Net.Http`, enabling `EnableSensitiveDataLogging` or header HTTP logging would expose tokens or connection details. Documented as a runbook warning only; nothing enforces it.
- **R4** A malformed but non-blank connection string is detected only at first use, not at startup.
- **R5** The authorization code is briefly visible in the callback URL before the client replaces the history entry.
- **R6** The browser access token carries six audiences (SEC-12); a leaked token works against every host for its lifetime.
- **R7** The deployment target and secret store are undefined (SEC-10 open item), so deployment-time secret handling is unreviewed.
- **R8** The CI job's per-job password is masked in logs, but any step that prints the connection string in a transformed form (for example base64) would bypass the mask.
