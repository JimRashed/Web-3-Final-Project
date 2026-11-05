# Milestone 3 local runbook (D01)

How to run the six services and the React client on one Windows machine from a clean checkout. Issue [#62](https://github.com/JimRashed/Web-3-Final-Project/issues/62).

## Prerequisites

| Tool | Version used | Check |
|---|---|---|
| .NET SDK | 10.0.x | `dotnet --version` |
| SQL Server LocalDB | 2019 (15.0) or later; verified on 2025 (17.0) | `sqllocaldb versions` |
| Docker Desktop (Compose v2) | verified on 29.1 | `docker --version` |
| Node.js | 22+ (verified on 22.20) | `node --version` |
| Keycloak | 26.7.5, pinned in `infra/compose.yaml` (pulled by Docker) | — |

To use a different SQL Server instead of LocalDB, set `QUIZARENA_SQLSERVER` to a connection string **without** a database name, for example `Server=localhost,1433;User ID=sa;Password=…;TrustServerCertificate=True`. The scripts append `Database=QuizArena<Service>Db` for each service.

## One-time setup

```powershell
git clone https://github.com/JimRashed/Web-3-Final-Project.git
cd Web-3-Final-Project
Copy-Item .env.example .env   # fill in the three blank values; .env is git-ignored, never commit it
```

## Start everything

```powershell
./scripts/start-milestone3.ps1 -ProvisionUsers   # first run: also creates the demo players
./scripts/start-milestone3.ps1                   # later runs
```

The script runs these steps in order, and stops with a clear message if one fails:
1. Checks the prerequisites and that `.env` has every required key. Values are never printed.
2. `docker compose up -d` for Keycloak, then waits for `http://localhost:9000/health/ready`.
3. With `-ProvisionUsers`, creates the demo players `player-a` and `player-b`. Their password is `QUIZARENA_DEMO_PASSWORD` from `.env`. This step is safe to repeat.
4. `dotnet build`, then `dotnet-ef database update` for each service. It only ever applies pending migrations and never drops data.
5. Starts each API in Development on its own port, and the client on 5173. Logs go to `.run/logs/`.
6. Waits for every `/health` and prints a status table.

Then open http://localhost:5173 and sign in.

| Component | URL | Health |
|---|---|---|
| Keycloak (realm `quizarena`) | http://localhost:8080 | http://localhost:9000/health/ready |
| Identity | http://localhost:5101 | `/health` |
| Statistics | http://localhost:5102 | `/health` |
| QuestionBank | http://localhost:5103 | `/health` |
| Lobby | http://localhost:5104 | `/health` |
| Gameplay | http://localhost:5105 | `/health` |
| Notification | http://localhost:5106 | `/health` |
| React client | http://localhost:5173 | `/` |

Useful options:
- `-Services Identity,Statistics` starts only those services. Each service starts independently. Only the Gameplay *workflow* needs its peers (Lobby, QuestionBank, Statistics, Notification) at request time.
- `-SkipFrontend`, `-SkipKeycloak` and `-SkipMigrations` skip the matching step.
- `-TimeoutSeconds 180` allows a longer wait on a slow first start.

## Stop

```powershell
./scripts/stop-milestone3.ps1             # the APIs and the client
./scripts/stop-milestone3.ps1 -Keycloak   # also stop the Keycloak container
```

Stopping never deletes anything. Keycloak keeps the `quizarena_keycloak-data` volume, and each service keeps its database, so the next start resumes with the same players, profiles, results and catalog. Nothing in these scripts resets data. Reset is a deliberate manual step (below).

## Configuration

Each API reads `appsettings.Development.json`, and any value can be overridden with an environment variable (`__` separates sections):

| Setting | Development value | Override |
|---|---|---|
| Port | 5101–5106 | `--urls` / `ASPNETCORE_URLS` |
| Database | LocalDB `QuizArena<Service>Db` | `ConnectionStrings__DefaultConnection` (EF tools: `QUIZARENA_<SERVICE>_CONNECTION`) |
| Token authority | `http://localhost:8080/realms/quizarena` | `Authentication__Authority` |
| Audience | `quizarena-<service>` | `Authentication__Audience` |
| Allowed browser origin | `http://localhost:5173` | `Cors__AllowedOrigins__0` |
| Gameplay machine-client secret (Gameplay only) | empty in source | `Authentication__MachineClientSecret`. The start script sets it from `QUIZARENA_GAMEPLAY_CLIENT_SECRET` in `.env` for the Gameplay process only |

The client's service URLs and OIDC settings are `VITE_*` variables. See [frontend setup](frontend-setup.md#public-configuration). The local Node/Vite server only serves the client's assets and has no API of its own. The Gameplay machine-client secret lives only in `.env`, and Keycloak reads it on first import (see [authentication](authentication.md#local-setup)).

## End-to-end journeys (D03)

With the stack running (`./scripts/start-milestone3.ps1 -ProvisionUsers` once) and Microsoft Edge installed:

```powershell
cd tests/e2e; npm ci
npm run journey:ui     # two players through the React screens
npm run journey:api    # the same game through the APIs with real Keycloak tokens
npm run journey:retry  # Statistics down while the match finishes, then back: the outbox must redeliver
```

The scripts sign in as `player-a` (host) and `player-b` (guest) using `QUIZARENA_DEMO_PASSWORD` from `.env` and never print it. `journey:retry` stops and restarts the Statistics API itself and updates `.run/pids.json`, so `stop-milestone3.ps1` still stops it.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `.env is missing` / `.env has no value for …` | Copy `.env.example` to `.env` and fill in every blank value |
| `docker compose up failed` | Start Docker Desktop and wait until it reports *running* |
| Keycloak not ready after 120 s | Rerun with `-TimeoutSeconds 240`, or check `docker compose -f infra/compose.yaml logs keycloak` |
| `Migrations failed for <Service>` | Read `.run/logs/migrate-<Service>.log`. Usually LocalDB isn't running: `sqllocaldb start MSSQLLocalDB` |
| `port … already in use, skipping` | A previous run is still up. Run `./scripts/stop-milestone3.ps1` first, or find the owner with `Get-NetTCPConnection -LocalPort <port>` |
| API returns 401 in the browser | The session expired (tokens last 5 minutes and refresh silently). Sign in again. A full page reload also signs you out, because tokens are kept in memory only. |
| CORS error in the browser console | The client must run on http://localhost:5173, or add your origin with `Cors__AllowedOrigins__0` |
| Sign-in page says invalid username or password | Run once with `-ProvisionUsers` |

**Reset (destructive, only on purpose):** `docker compose --env-file .env -f infra/compose.yaml down -v` removes Keycloak's data, and `sqllocaldb stop MSSQLLocalDB` followed by deleting the `QuizArena*Db` databases removes the service data. The scripts never do either.

## Validation record

These runs were on 2026-11-04 (Windows 11, LocalDB 17.0, Docker 29.1, Node 22.20, .NET SDK 10), against the milestone branch at `3a9b9d2`. No secret values were recorded.

| Check | Result |
|---|---|
| `start-milestone3.ps1` (all components; Keycloak already running) | finished in 41 s, all eight components `ok`, script exited |
| `stop-milestone3.ps1` | stopped all 7 processes, ports 5101–5106 and 5173 freed |
| Stop everything including Keycloak, then start again | row counts identical before and after: results 2, profiles 1, questions 5 (data preserved, no reset) |
| `-Services Identity -SkipFrontend` | only 5101 listening, Identity `ok` (services start independently) |
| **Clean checkout** (fresh `git clone` of the milestone branch, only `.env` copied, no `node_modules` or `.run`), run by Carter on 2026-11-05 | first attempt: all APIs and Keycloak `ok` but **Client NOT READY**, because the script's `npm ci` failed silently through the `npm.ps1` shim. After the fix in this PR: `start-milestone3.ps1 -ProvisionUsers` completed in 66 s with all 8 components `ok`, and from the same clone `journey:ui` 11/11, `journey:api` 10/10, `journey:retry` 8/8 |
| `npm run journey:ui` (browser, both players) | sign-in through Keycloak; lobby create, join by code, both see 2 of 2, guest's host controls disabled; host lock and start; **guest opens the match from the delivered `MATCH_CREATED` event in-app, still signed in**; each player sees their own score; 5 rounds by clicking answers; end screens "You won"/"You lost"; both see the match in Statistics; guest gets `MATCH_COMPLETED` with "View result"; no 5xx responses: **11/11** |
| `npm run journey:retry` | Statistics stopped before the last round; match still finishes; both results stay Pending with `transport failure` and retries while the 4 Notification events are delivered; after Statistics restarts the outbox delivers them (guest games +1) and every row ends Delivered: **8/8** |
| Two-player API journey with real Keycloak tokens (`player-a` hosts, `player-b` joins) | lobby create/join/lock, Gameplay start verified against Lobby and QuestionBank with machine tokens, guest start refused (403), repeated start returns the same match, 5 rounds with both players answering simultaneously, match finished, the outbox delivered both results to Statistics and `MATCH_CREATED`/`MATCH_COMPLETED` to both players in Notification: **10/10** (after the fixes in this PR; before them every start returned 503) |

**Still needed for acceptance:** another team member follows this runbook from a clean checkout and records the commands, ports and redacted health results here.
