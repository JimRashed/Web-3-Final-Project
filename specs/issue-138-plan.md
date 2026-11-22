# Issue #138 plan: configure and verify environment-specific secrets (SEC-A2)

Status: agent-reviewed plan (Dispatcher pipeline). Not human-approved.
Issue: https://github.com/JimRashed/Web-3-Final-Project/issues/138
Base: `feature/milestone-4-security-and-cross-device-profiles` @ fb60334. Branch: `feat/138-configure-environment-secrets`.

## 1. Summary and architecture

Goal: every service gets its environment-specific settings through the existing entry points
(`appsettings*.json`, `.env.example`, `src/QuizArena.Client/.env.example`, `infra/oidc/`), secrets
come only from the SEC-10 store for each environment, a missing or invalid required setting stops
the host at startup with a message that names the key (never the value), and the docs describe the
local, CI and deployment setup.

Binding decisions (from `docs/milestone3/security-decisions.md`):
- SEC-10: local = git-ignored `.env` injected by `scripts/start-milestone3.ps1` (user-secrets optional);
  CI = GitHub Actions; deployment target undefined (open item). This plan stays inside SEC-10.
- SEC-05/06: picture storage is Identity's `ConnectionStrings:DefaultConnection`. No new storage key,
  no new secret.
- SEC-03/08: no storage key, URL or token in profile data or image `src`. Picture endpoints are #144/#145.

Notes for the record:
- #62 (documented local environment) is still open. It was overridden by the user; the docs must
  say it is pending.
- #137's approval record lacks SuSu's review. Do not mark any SEC decision approved.

Layer boundaries:
- API hosts (`src/QuizArena.<Service>.Api`): options validation in the shared `Security/` files
  and in the Gameplay `Program.cs`. No domain or data-layer changes beyond what exists.
  The persistence extensions already reject a blank connection string.
- CI (`.github/workflows/ci.yml`): backend job only. The frontend job is untouched.
- Client: tests only. Its configuration (`config.ts`) already validates public settings.
- Docs: runbook, authentication, database-operations, plus a new review note.

### Design choices

1. CI secret: the ephemeral password is generated at runtime. GitHub `services:` containers take
   their env when the job starts, before any step runs, so the `sqlserver` service block is removed.
   A new step `Start SQL Server` does the following:
   - Generates a password with `openssl rand -base64 24`, keeping only `[A-Za-z0-9]`. It adds a
     fixed complexity prefix so SQL Server's policy is always met; the prefix alone is not a secret.
   - Runs `echo "::add-mask::$password"` before anything else uses the value.
   - Starts `mcr.microsoft.com/mssql/server:2022-latest` with `docker run -d --name sqlserver -p 1433:1433`
     and `-e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD`. The value is passed from the step's environment by
     name only (`-e MSSQL_SA_PASSWORD` with no value), so it never appears on the command line.
   - Appends `QUIZARENA_TEST_SQLSERVER=Server=127.0.0.1,1433;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False`
     to `$GITHUB_ENV`.

   Then:
   - The job-level `QUIZARENA_TEST_SQLSERVER` literal is removed.
   - The existing `Wait for SQL Server` step stays. It runs after `Start SQL Server`.
   - A final `if: always()` step runs `docker rm -f sqlserver` (optional; the runner is discarded anyway).

   Why: it commits no literal and needs no repo admin action. Forks and PRs from forks keep working
   because no secret is required. The value lives only for one job. We rejected
   `${{ secrets.X }}` with a fallback: the fallback would need its own generator anyway, so it adds a
   path with no benefit.
2. Fail-safe startup in the shared `Security/SecurityExtensions.cs`, changed identically on all six
   hosts. The files differ only in namespace and the `<Service>Policies.All` reference. The checks
   are chained onto the existing `AddOptions<QuizArenaAuthenticationOptions>()` and use
   `Validate<IHostEnvironment>` / `Validate<IHostEnvironment, IConfiguration>` overloads. The public
   signature `AddQuizArenaSecurity(IServiceCollection, IConfiguration)` stays unchanged, so the six
   `Program.cs` files do not change for this. These checks are lazy, which means
   `WithWebHostBuilder` + `AddInMemoryCollection` overrides are visible to them.
   - All environments: the existing Authority rule becomes "absolute URI with scheme http or https".
   - Outside Development: the Authority scheme is https.
   - Outside Development: `RequireHttpsMetadata` is true.
   - Outside Development: `Cors:AllowedOrigins` has at least one entry, and every entry is an
     absolute http/https URI. This is read from `IConfiguration`. All six hosts serve the browser:
     each `appsettings.Development.json` lists `http://localhost:5173`.
   - Messages name the key only, for example
     "Authentication:Authority must use https outside the Development environment."
3. Gameplay-only validation (`Program.cs` + `Services/GameplayServiceOptions.cs`, `Services/MachineTokens.cs`):
   - `services.Configure<GameplayServiceOptions>` becomes
     `AddOptions<GameplayServiceOptions>().Bind(...).Validate(...).ValidateOnStart()`. Each of `Lobby`,
     `QuestionBank`, `Statistics` and `Notification` must be an absolute http/https URI. One rule per
     key gives the message "ServiceUrls:Lobby must be an absolute http or https URI." and so on.
   - `MachineTokenOptions`: `AddOptions<MachineTokenOptions>().Bind(...)` with
     `Validate<IHostEnvironment>((o, env) => env.IsDevelopment() || !string.IsNullOrWhiteSpace(o.MachineClientSecret),
     "Authentication:MachineClientSecret is required outside the Development environment.")`
     and `.ValidateOnStart()`.
   - In Development a blank secret is still allowed. It keeps the lazy 503 path, so the existing
     integration tests (which run as Development without a secret) keep passing.
   - A static helper `IsHttpUri(string?)` sits in `GameplayServiceOptions`, so the rule is unit-testable.
4. Connection strings: no code change. All six `*PersistenceExtensions.cs` already throw
   "ConnectionStrings:DefaultConnection is required ..." before `Build()`. Tests prove it. A malformed
   connection string fails at first use, and SqlClient's error does not contain the password. This is
   recorded as a residual risk; no new parsing is added.
5. Error leak test: a real `KeycloakMachineTokenSource` + real `LobbyHttpGateway`. The token endpoint is
   a stub that returns 400 with a body echoing a sentinel. The test checks that the 503 problem body
   contains neither the sentinel secret nor the IdP body.

## 2. File inventory

Modify (production/config):
- `.github/workflows/ci.yml`: remove the `services.sqlserver` block and the `QUIZARENA_TEST_SQLSERVER` literal. Add the `Start SQL Server` step (masked runtime password exported via `GITHUB_ENV`) and the cleanup step. Frontend job unchanged.
- `src/QuizArena.{Identity,Statistics,QuestionBank,Lobby,Gameplay,Notification}.Api/Security/SecurityExtensions.cs` (6 files): add the identical validation chain from design choice 2. Add `using Microsoft.Extensions.Hosting;` if needed.
- `src/QuizArena.Gameplay.Api/Program.cs`: add `ValidateOnStart` registrations for `GameplayServiceOptions` and `MachineTokenOptions`.
- `src/QuizArena.Gameplay.Api/Services/GameplayServiceOptions.cs`: add the `IsHttpUri` helper. The validation messages are constants.
- `.env.example` (root): comment-only clarifications. The Gameplay secret goes only to Gameplay; CI generates its own SQL password. No new keys.

Create (tests):
- `tests/QuizArena.Gameplay.IntegrationTests/Security/StartupConfigurationTests.cs`: startup-failure tests for Gameplay (Authority, ServiceUrls, secret, https, RequireHttpsMetadata, CORS, connection string). Covers the shared Security files as the representative host.
- `tests/QuizArena.Identity.IntegrationTests/Security/StartupConfigurationTests.cs`: Identity startup failures for a missing `ConnectionStrings:DefaultConnection` (picture storage, per SEC-05) and an invalid Authority. Also shows that a valid Production-style configuration starts.
- `tests/QuizArena.Gameplay.IntegrationTests/Errors/MachineTokenLeakTests.cs`: checks that the 503 body from a token failure carries no secret and no IdP body.
- `tests/QuizArena.Gameplay.UnitTests/Services/GameplayServiceOptionsTests.cs`: `IsHttpUri` cases.

Modify (tests):
- `src/QuizArena.Client/src/config.test.ts`: `.env.example` (imported via `?raw`) contains only `VITE_*` keys and no secret-like names.
- `src/QuizArena.Client/src/auth/session.test.ts`: the sign-in handoff stores only the matchId. The callback destination has only `matchId` and no token/code/state. Snapshots carry no token.
- `src/QuizArena.Client/src/features/lobby/LobbyPage.test.tsx` (or `MatchPage.test.tsx`): the match navigation/share link has exactly one query parameter, `matchId`, and does not contain the access token.
- `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileEndpointsTests.cs`: the `/api/users/me` JSON property set is exactly `id, issuer, subject, displayName` (no token-like field). Skip if an equivalent assertion already exists.

Docs:
- `docs/milestone3/runbook.md`: new section "Environment configuration (SEC-A2, #138)".
- `docs/milestone3/authentication.md`: short "Startup validation" subsection under API token validation, linking the runbook.
- `docs/milestone3/database-operations.md`, Configuration: picture storage reuses Identity `DefaultConnection`, the blank-connection-string failure, and the CI runtime password.
- Create `docs/milestone3/configuration-review.md`: log/error review note and residual risks for #139.
- `docs/milestone3/security-decisions.md`: add one line under "Threat-model hand-off to A3 (#139)" linking `configuration-review.md`. No change to the decision rows or the approval status.

Not touched: domain/data projects, contracts, `infra/compose.yaml`, realm JSON (already placeholder/fail-fast), `scripts/start-milestone3.ps1` (already checks keys without printing; Development forced), client production code.

## 3. Ordered steps

1. **CI ephemeral password.** Files: `ci.yml`. Delete `services:`. Remove the `QUIZARENA_TEST_SQLSERVER` job env line and keep the DOTNET_* env. Insert `Start SQL Server` before `Wait for SQL Server`, and put the shell logic in that step's `run:` (bash, `set -euo pipefail`). The password is generated and masked first; then the container starts; then the connection string is appended to `$GITHUB_ENV`. Add an `if: always()` cleanup step.
   Check: `grep -n "Password=" .github/workflows/ci.yml` shows only the `${...}` interpolation, no literal. The YAML parses. The frontend job is byte-identical (`git diff` touches only the backend job).
2. **Shared security validation.** Files: the 6 `SecurityExtensions.cs`.
   - Change the Authority rule to http/https absolute.
   - Add `Validate<IHostEnvironment>` rules: Authority uses https outside Development, and `RequireHttpsMetadata` is true outside Development.
   - Add a `Validate<IHostEnvironment, IConfiguration>` rule: `Cors:AllowedOrigins` is non-empty with valid http/https URIs outside Development.
   - Keep the eager `origins` read for `AddCors` as it is.
   Check: a normalized diff across the six files (namespace and `<Service>Policies` replaced) is empty. The build passes with `-warnaserror`.
3. **Gameplay options validation.** Files: `Program.cs`, `GameplayServiceOptions.cs`. Register as in design choice 3. Check: `KeycloakMachineTokenSource` still reads `IOptions<MachineTokenOptions>`, and the lazy check in `MachineTokens.cs:106-110` stays as defence in depth.
4. **Startup-failure tests.** Files: the two `StartupConfigurationTests.cs`.
   - Pattern: build from a new `WebApplicationFactory<Program>`, or reuse `SecuredApiFactory`, then call `WithWebHostBuilder`:
     - `builder.UseEnvironment(...)`
     - `builder.UseSetting(...)` for eagerly read keys (`ConnectionStrings:DefaultConnection`, `Cors:AllowedOrigins:0`). `IdentityApiFactory` documents that Program reads these before app-configuration overrides apply.
     - `ConfigureAppConfiguration(... AddInMemoryCollection ...)` for options-bound keys.
   - Act with `factory.CreateClient()` inside `Action`, then assert `.Should().Throw<Exception>()`.
   - A private helper flattens `InnerException` / `AggregateException` / `OptionsValidationException.Failures` messages.
   - Each test asserts:
     - the message contains the key name;
     - the message does not contain the sentinel value used (for example a unique `sentinel-<guid>` secret or URL).
   - Production-style tests supply a full valid baseline (https Authority, `RequireHttpsMetadata=true`, origins, connection string, ServiceUrls, secret), so exactly one key is wrong in each case.
   - Gameplay tests run against the test-only `Production` environment and use `ConfigureTestServices` to remove `IHostedService`, so no outbox polling happens.
5. **Token-failure leak test.** File: `MachineTokenLeakTests.cs`. Start from `GameplayApiFactory` + `WithWebHostBuilder`:
   - Set `Authentication:MachineClientSecret` to the sentinel.
   - In `ConfigureTestServices`, re-register the real `ILobbyGateway` → `LobbyHttpGateway` (the factory replaces it with a fake), and add `AddHttpClient(MachineTokenOptions.HttpClientName).ConfigurePrimaryHttpMessageHandler(...)` with a stub that returns 400 and the body `{"error":"invalid_client","echo":"<sentinel>"}`.
   - Call `POST /matches` with a valid user token.
   - Assert: 503, the body contains no sentinel and no `invalid_client`, and the body has no `exception`/`stackTrace` members.
   - Run under both Development and Production (Theory), because Development problem details may add exception detail. If Development leaks, fix `GameplayExceptionHandler` to stop passing `Exception` into `ProblemDetailsContext`, and note it in the review doc.
6. **Unit test.** `GameplayServiceOptionsTests.IsHttpUri_*`:
   - accepts `http://x` and `https://x/`;
   - rejects blank, relative, `ftp://`, `file:///` and `not-a-url`.
7. **Client tests.**
   - `config.test.ts`: `import envExample from '../.env.example?raw'` (typed by `vite/client`; no `node:fs`, because tsconfig `types` excludes node). Every non-comment, non-blank line matches `^VITE_[A-Z0-9_]+=`, and no key matches `/SECRET|PASSWORD|TOKEN|KEY/i`.
   - `session.test.ts`: call `login()` from `/matches?matchId=m-1` with an adapter whose `token` is a sentinel. Then check that:
     - sessionStorage holds only `quizarena.login.matchId` = `m-1`;
     - `completeLoginRedirect()` returns a URL whose only search param is `matchId`, and nothing contains the sentinel;
     - the `getSnapshot()` JSON does not contain the sentinel.
   - Lobby test: the navigated URL is `/matches?matchId=<id>`, with a single param and no token.
8. **Profile contract assertion.** Identity endpoint test: the profile response's JSON property set is exactly `id, issuer, subject, displayName`.
9. **Docs.**
   - `runbook.md`, new section "Environment configuration (SEC-A2, #138)":
     - (a) **Local.** Create `.env` from `.env.example`. The start script passes the Gameplay secret only to Gameplay. User-secrets are optional. #62 is still pending.
     - (b) **CI.** No repository secret is needed: the SQL password is generated per job and masked. Future CI secrets go in GitHub Actions secrets (SEC-10).
     - (c) **Deployment.** This is an open item per SEC-10. List the env-var names a deployment must provide (`Authentication__Authority`, ...). Outside Development, startup fails unless all of these are set:
       - an https Authority;
       - `RequireHttpsMetadata=true`;
       - CORS origins;
       - the connection string;
       - for Gameplay, ServiceUrls and the secret.
     - (d) **Per-service key matrix (names only).**
       - All six services: Authority, Audience, BrowserClientId, MachineClientId, RequireHttpsMetadata, Cors:AllowedOrigins and ConnectionStrings:DefaultConnection.
       - Gameplay also needs ServiceUrls:* and MachineClientSecret.
       - Identity's DefaultConnection is also the picture storage.
       - Client: the public `VITE_*` keys.
       - Keycloak/compose: `KC_BOOTSTRAP_ADMIN_*`, `QUIZARENA_GAMEPLAY_CLIENT_SECRET`, `QUIZARENA_DEMO_PASSWORD`.
       - Each row has a secret/public column and a source column.
     - (e) **Rotation.**
       - Gameplay client secret: change it in the Keycloak admin console, then in `.env`, then restart Gameplay. The realm import applies only on the first import.
       - Demo password: re-provision the demo users.
       - CI: nothing to rotate.
     - (f) **Warning.** Outside local debugging:
       - never raise `Logging:LogLevel` to Debug or Trace for `Microsoft.AspNetCore.Authentication`, `Microsoft.EntityFrameworkCore` or `System.Net.Http`;
       - never enable `EnableSensitiveDataLogging`;
       - never enable HttpLogging of headers.
   - `configuration-review.md`:
     - What was reviewed: problem bodies per host, exception handlers, adapter logs, the token client, the start script, compose, and client storage.
     - Findings, with file:line references.
     - Residual risks for #139:
       - full exceptions in logs show the SQL host, database and user (no password);
       - adapter logs may show the internal host:port;
       - the Debug-level risk;
       - a malformed connection string is only detected at first use;
       - the auth code appears briefly in the callback URL;
       - the SEC-12 multi-audience token;
       - the deployment store is undefined.
     - Mark it as agent review evidence, not approval.
   - `authentication.md` / `database-operations.md`: short subsections linking the runbook (see section 2).
10. **Validate** (section 6). Record the command results in the PR, never secret values.

## 4. Acceptance coverage

| # | Criterion | Steps | Evidence |
|---|---|---|---|
| AC1 | Environment-specific authority/audiences, service credentials, Identity DB, picture storage via existing entry points | 2, 3, 9d | Existing appsettings/.env.example entry points. Key matrix. Picture storage = Identity DefaultConnection (SEC-05), with no new key. |
| AC2 | Approved secret store, safe placeholders, credentials scoped to services | 1, 9a-c | CI literal removed (masked runtime password). Local `.env` per SEC-10. Gameplay secret goes only to Gameplay (start script :123-131). Placeholders stay blank. |
| AC3 | Absent/invalid required setting fails safely | 2, 3, 4, 6 | StartupConfigurationTests cover Authority, https, RequireHttpsMetadata, CORS, ServiceUrls, the secret and the connection string. Messages name keys only. |
| AC4 | Document local, CI, deployment setup | 9 | Runbook section a-f. Deployment is an open item. #62 pending. |
| AC5 | Public React config contains only public settings | 7 | `config.test.ts` `.env.example` check, plus the existing `config.ts` validation. |
| AC6 | Errors/logs reviewed for leaked credentials/storage internals | 4, 5, 9 | MachineTokenLeakTests. Startup messages exclude sentinels. configuration-review.md. |
| AC7 | No bearer token in profile data, image URLs, handoff links | 7, 8, 9 | Session and lobby tests. Profile property-set test. Image URLs: the endpoints are not implemented yet; SEC-03/08 binds #144/#145 to Blob/object URLs (recorded in the review doc). |

## 5. Testing strategy

- xUnit + FluentAssertions, AAA comments and `Method_Condition_Result` names, in the existing test projects.
- Startup-failure tests fail before any DB access. The valid-baseline Identity test only starts the host (no migration). It uses the LocalDB/`QUIZARENA_TEST_SQLSERVER` server string.
- The leak test runs on TestServer with a stub `HttpMessageHandler` and no network.
- Client tests use Vitest + Testing Library with the existing doubles (`src/test/doubles.ts`).
- Existing tests stay green. They run as Development and supply ServiceUrls, either from `GameplayApiFactory` or from `appsettings.Development.json`.

## 6. Validation commands

From `C:/Web-3-Final-Project-wt/138`:

```
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal
  (LocalDB; if parallel LocalDB timeouts recur, run each test project separately)
cd src/QuizArena.Client; npm ci; npm run typecheck; npm run lint; npm run test -- --run; npm run build
node contracts/validate-contracts.mjs
python -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"   (or any YAML parser)
git grep -n "QuizArena!Ci" -- .github    (expect no match)
```

Also run the normalized six-file diff of `SecurityExtensions.cs`. Record actual output only; never quote historical test counts.

## 7. Risks and out of scope

Risks:
- The CI step can only be proven on the PR's own CI run. The password must meet SQL complexity (fixed prefix), and the mask must be emitted before any use.
- Non-Development runs without full configuration now fail at startup. This is intended and documented. `start-milestone3.ps1` forces Development.
- The CORS rule applies to all six hosts. A future machine-only host would need an opt-out.
- Development problem details may include exception data. Step 5 detects this and fixes it.
- `WebApplicationFactory` timing: use `UseSetting` for keys that are read before `Build()`.
- #62 is open (user override), and #137's approval lacks SuSu's review. Neither blocks implementation; both are stated in the docs and the PR.

Out of scope:
- choosing a deployment target or store (SEC-10 open item);
- picture endpoints (#144/#145);
- the DR-1..DR-5 fixes (#140/#141/#143);
- the threat model itself (#139 receives only the risk list);
- new storage keys or secrets;
- user-secrets wiring / `UserSecretsId`;
- realm JSON and compose changes;
- marking any SEC decision approved.

## 8. Plan adjustments (implementation)

Recorded by the implement stage; no file outside the inventory was added.

- `src/QuizArena.Client/src/config.test.ts`: the `?raw` import of `.env.example` is refused by Vite (`server.fs.deny` covers `.env.*`). The test reads the file with `node:fs` from `process.cwd()` (the client root, which is the Vitest root locally and in CI). `npm run typecheck` passes because the Node types are already loaded through Vite's own type references. The Vite config is not changed.
- `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileEndpointsTests.cs`: not modified. `PutMe_ThenGetMe_ReturnsProfileForTokenIdentity` already asserts the exact property set `id, issuer, subject, displayName` (step 8 "skip if an equivalent assertion already exists").
- `GameplayExceptionHandler`: not modified. `MachineTokenLeakTests` passes under Development and Production, so the Development problem body carries no exception detail.
- Lobby client test: added to `LobbyPage.test.tsx` (not `MatchPage.test.tsx`).
- Startup tests pass every key through `UseSetting` (host-level settings reach both the eager reads in `Program` and the lazily validated options). The Gameplay ones remove only the `DeliveryWorker` hosted service, not every `IHostedService`. The leak test sets `ConnectionStrings:DefaultConnection` with `UseSetting` because the Production run has no appsettings value for it.
