# QuizArena

A two-player quiz game built as six independently runnable, layered ASP.NET Core (.NET 10) services with a React client and Keycloak sign-in.

**Milestone 3 report:** [docs/milestone3/architecture-and-reflection.md](docs/milestone3/architecture-and-reflection.md)

## Layout

| Path | Contents |
|---|---|
| `src/QuizArena.<Service>.{Domain,Data,Api}` | Identity, Statistics, QuestionBank, Lobby, Gameplay, Notification. Each has its own database and no references to other services. |
| `src/QuizArena.Client` | React 19 + TypeScript + Vite client (OIDC authorization code + PKCE) |
| `contracts/openapi/` | OpenAPI contract per service |
| `infra/` | Local Keycloak (Docker Compose) and the realm import |
| `tests/` | Unit, integration, security and architecture tests per service |
| `specs/` | Per-issue plans |
| `src/QuizArena.{Api,Data,Domain}` | Milestone 2 aggregate host, kept for the legacy migration (D02) |

## Run locally

Prerequisites: .NET 10 SDK, SQL Server LocalDB (or set `QUIZARENA_TEST_SQLSERVER`), Docker Desktop, and Node 22+.

```powershell
Copy-Item .env.example .env          # fill in the blank values; .env is git-ignored
docker compose --env-file .env -f infra/compose.yaml up -d
node --env-file=.env infra/oidc/provision-demo-users.mjs
dotnet build QuizArena.sln
dotnet test QuizArena.sln
cd src/QuizArena.Client; npm ci; npm run dev
```

Each API runs with `dotnet run --project src/QuizArena.<Service>.Api` on ports 5101–5106. Database and sign-in details are in [database operations](docs/milestone3/database-operations.md), [authentication](docs/milestone3/authentication.md) and [frontend setup](docs/milestone3/frontend-setup.md).

## Documentation

- Milestone 3: [report](docs/milestone3/architecture-and-reflection.md), [architecture decisions](docs/milestone3/architecture-decisions.md), [API contracts](docs/milestone3/api-contracts.md), [authorization matrix](docs/milestone3/authorization-matrix.md), [work allocation](docs/milestone3/work-allocation.md), [validation results](docs/milestone3/validation-results.md)
- Milestone 2 (historical): [architecture notes](docs/architecture-notes.md), [API contracts](docs/api-service-contracts.md), [database schema](docs/database-schema.md), [reflection](docs/reflection.md)
