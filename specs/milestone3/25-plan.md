# #25 — CI gates for architecture, backend, frontend and security tests

> Issue: #25 (P10) · Parent feature: #8 (F00) · Epic: #7 (E00) · Owner: @SuSu-Lisu
> Depends on #24 (test hosts) and #22 (frontend shell), both merged.

## Why

The development VM cannot run `dotnet test`: the VSTest host fails with
`Access is denied` when it registers a parent-process exit callback, and SQL Server LocalDB is
unreachable for the same class of restriction. Every relational and security test would otherwise
be "run it on someone else's machine". A GitHub Actions workflow makes the evidence reproducible
and attached to every pull request.

## What the workflow does

`.github/workflows/ci.yml`, two jobs:

**backend** — ubuntu runner with a SQL Server 2022 service container:
1. `dotnet restore`
2. `dotnet build --no-restore -warnaserror` (audit disabled for deterministic builds)
3. wait for SQL Server, then `dotnet test --no-build`
4. upload the `.trx` results as an artifact

`QUIZARENA_TEST_SQLSERVER` points at the service container, so the per-test database fixture from
#24 creates and drops its own databases.

**frontend** — Node 22 with npm cache:
`npm ci` → `typecheck` → `lint` → `test -- --run` → `build`.

## Gate mapping

| Human gate check | Evidence produced by CI |
|---|---|
| "Does the solution build cleanly?" | build step, `-warnaserror` |
| "Do all automated tests pass?" | backend test step + uploaded `.trx` |
| "Are dependencies one-way?" | `QuizArena.ArchitectureTests` inside the backend test run |
| "Do authentication tests cover missing/invalid tokens and scopes?" | per-service security matrix tests |
| "Frontend builds and is type-safe" | frontend job |

## Known constraints

- `-p:NuGetAudit=false` keeps the build deterministic; dependency advisories are reviewed by the
  pinned versions instead (see `System.Security.Cryptography.Pkcs` pin in the Data projects).
- The workflow runs on GitHub-hosted runners only. It does not start Keycloak: the security tests
  use the ephemeral token fixture (#24) rather than a live provider.
- End-to-end checks against a running Keycloak remain a D03 manual step.

## Follow-up

- Add coverage collection and a threshold once the suites stabilise.
- Add the end-to-end job when the Playwright suite exists (#25 lists `test:e2e`).
