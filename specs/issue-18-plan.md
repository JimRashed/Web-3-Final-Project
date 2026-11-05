# Issue #18 (P03): six service hosts and solution boundaries

Status: agent-reviewed plan for the Dispatcher run; implementation and final human PR review pending. Issue: https://github.com/JimRashed/Web-3-Final-Project/issues/18. Parent feature #8; epic #7. Base: `feature/milestone-3-layered-service-development` at `f5176fb`, which includes merged P02 PR #70.

## Summary and architecture

Create an independently runnable .NET 10 `Domain`/`Data`/`Api` triple for each of Identity, Statistics, QuestionBank, Lobby, Gameplay and Notification. Follow P02 [ADR-01/02 and configuration contract](../docs/milestone3/architecture-decisions.md): Domain is pure, Data refers to its own Domain, and Api refers only to its own Data/Domain. Each Api initially owns only public `GET /health`, returning a constant safe liveness response without a database, peer call, authentication or secret. Development ports are 5101 through 5106 in P02 order; normal ASP.NET Core URL configuration can override them. These hosts establish composition roots and deployment boundaries. They do not claim to implement the other P02 API operations.

The old `QuizArena.Api`, `.Data`, `.Domain` and aggregate tests stay in the solution and retain their current runtime behavior while later issues migrate it. Copying legacy endpoints or attaching the shared `QuizArenaContext` to new hosts would create duplicate ownership. P04 owns per-service EF contexts/migrations; P06 owns JWT policies; the service leaf issues own business rules/routes; D02 owns retirement of the aggregate projects. No frontend exists yet (P07 owns it). The historical issue text's intermediate human gates and `specs/milestone3/<n>-plan.md` location are superseded by the current Dispatcher policy: this scoped plan is `specs/issue-18-plan.md`, with human review at the final draft PR.

## File inventory

For each `S` in **Identity, Statistics, QuestionBank, Lobby, Gameplay, Notification**, create these exact paths (the six substitutions account for 36 production files and 18 test files):

| Path template | Purpose |
|---|---|
| `src/QuizArena.S.Domain/QuizArena.S.Domain.csproj` | `net10.0` pure library; no package, project, web or outer-layer references |
| `src/QuizArena.S.Data/QuizArena.S.Data.csproj` | `net10.0` library; only project reference is its own Domain |
| `src/QuizArena.S.Api/QuizArena.S.Api.csproj` | `net10.0` ASP.NET Core host; project references only its own Data and Domain |
| `src/QuizArena.S.Api/Program.cs` | Unique `QuizArena.S.Api.Program` entry point, composition root and `GET /health` |
| `src/QuizArena.S.Api/appsettings.Development.json` | Own development URL (`http://localhost:5101` through `:5106`), with no secret or database value |
| `src/QuizArena.S.Api/Properties/launchSettings.json` | Own Development launch profile and matching port |
| `tests/QuizArena.S.UnitTests/QuizArena.S.UnitTests.csproj` | Service-owned xUnit/FluentAssertions/Moq unit-test home, referencing own Domain only; no invented business-rule test while Domain is empty |
| `tests/QuizArena.S.IntegrationTests/QuizArena.S.IntegrationTests.csproj` | Service-owned xUnit/FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing` and own Api reference |
| `tests/QuizArena.S.IntegrationTests/HealthEndpointTests.cs` | Real HTTP pipeline check: anonymous 200 and safe response, no peer/DB fixture |

Also modify/create:

| File | Purpose |
|---|---|
| `QuizArena.sln` | Add all 30 service projects and architecture test project, preserving five legacy solution projects |
| `tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj` | xUnit project for enforceable dependency boundaries |
| `tests/QuizArena.ArchitectureTests/ServiceBoundaryTests.cs` | Discover all six triples and reject missing/incorrect projects, Domain package/web/outer dependencies, illegal Data/Api project references and cross-service references; reject backend assembly references in a future client project |
| `docs/milestone3/p03-service-scaffold.md` | Launch/configuration instructions, temporary legacy coexistence and ownership handoff to P04/P06/leaf tasks/D02 |
| `docs/milestone3/p03-validation.md` | Dispatcher-recorded exact build/test/host-smoke results and read-only review; human signoff explicitly pending |
| `specs/issue-18-plan.md` | This plan and any factual implementation adjustments |

Conditional build-gate repair: if fresh dependency analysis confirms the existing `System.Security.Cryptography.Pkcs` 9.0.11 advisory blocks the required `-warnaserror` build, update the narrowest existing project reference that fixes the transitive graph (likely `src/QuizArena.Data/QuizArena.Data.csproj`); record the path/version/evidence here before the change. Do not suppress NuGet audit or pin an unverified version. The explicit conditional path is not authorization for broad dependency upgrades.

No existing production code, aggregate tests, migrations, P02 contracts or historical plans are deleted or moved in P03.

## Ordered implementation steps

1. **Baseline and dependency graph.** Run `dotnet --info`, `dotnet restore QuizArena.sln`, a warning-as-error baseline build and focused baseline tests. Record existing failures. Inspect `dotnet list ... package --include-transitive` (or assets) if Pkcs advisories remain; select a patched compatible version through the normal package source and make only the necessary build-gate correction. Completion: evidence distinguishes existing failures from new ones; no advisory is disabled.
2. **Create the 18 production projects.** Use the exact P02 names and target framework. Keep empty Domain/Data libraries minimal; Api has only own Data/Domain project references. Give each Api a namespaced public `Program` entry point so test hosts can name it unambiguously. Completion: all project references resolve, no cross-service or legacy references appear, and every host has a distinct assembly/entry point.
3. **Create six composition roots and configuration.** Each `Program` creates and runs its own `WebApplication`, maps a single anonymous `GET /health` returning only a status indicator, and registers no shared context, peer client, business route, Swagger document or token-minting behavior. Development configuration/launch profile uses ports 5101–5106, while `--urls`/`ASPNETCORE_URLS` remains effective. Completion: each Api starts on its configured port with other APIs and SQL Server stopped; request `/health` returns 200 with safe body. Production HTTP security and non-health endpoints remain later-task work.
4. **Create twelve service-owned test projects and the architecture suite.** The six unit projects establish isolated homes for upcoming domain rules without placeholder assertions. Each integration project exercises its own Api via `WebApplicationFactory<QuizArena.S.Api.Program>` and checks anonymous health status/body/content without external services. The architecture tests parse/evaluate all service project files, resolve reference paths and enforce the exact permitted dependency sets. Include negative fixture cases that demonstrate rejection of a cross-service reference and a Domain NuGet/web reference; avoid a test that merely asserts the current files exist. Completion: architecture tests fail on those injected violations and pass on the intended graph; all six HTTP tests pass.
5. **Integrate and document.** Add projects to `QuizArena.sln` under source/test folders, retaining legacy projects until D02. Document launch commands/ports, coexistence and downstream ownership. Completion: solution lists the expected project count, old tests still run, new hosts expose only health, and validation evidence records exact commands/results with final human review pending.

## Acceptance coverage

| Issue requirement | Steps / evidence |
|---|---|
| Six independently buildable/runnable hosts, separate config/ports, peer-independent anonymous `/health` | 2–3; six host smoke requests and six HTTP integration tests |
| Dedicated Domain/Data/Api projects for every service; pure Domain and no frontend assembly references | 2, 4; exact project graph and architecture suite |
| No duplicate runtime ownership while code migrates incrementally | 2–3, 5; health-only new hosts, legacy untouched pending D02 |
| Six pairs of service test projects and architecture tests that reject prohibited references | 4; test discovery and mutation cases |
| File/layer ownership and applicable scope policies | 2–4; project graph, no business endpoints, anonymous health per P02; P06 owns route policies |
| Actual plan, build/test/host evidence and read-only review, with independent human approval pending | This plan, step 5 evidence and Dispatcher review/draft PR. No agent claims human signoff or closes #18 |

## Testing strategy and validation commands

From repository root, run and record exits/warnings/test counts for:

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj --no-build --verbosity normal
dotnet test QuizArena.sln --no-build --verbosity normal
```

Run each `src/QuizArena.S.Api/QuizArena.S.Api.csproj` separately with its Development profile (or explicit `--urls`) and request `http://localhost:<port>/health` while the other five hosts are stopped; record exact command/status/body and stop each process cleanly. Check ports are unused before launch. Run the existing aggregate integration suite with its provisioned SQL Server LocalDB; do not skip or silently replace relational tests. No frontend checks apply until P07 creates the client. Record `git diff --check`, changed paths, generated-file/secret scan and an independent read-only code review before publication. If environmental prerequisites prevent a check, report the specific blocker rather than claim a pass.

## Risks and out of scope

- Existing Pkcs 9.0.11 NU1902/NU1903 advisories may block the required warning-as-error build. Correct the narrow transitive dependency only after verification; no audit suppression. Package availability or LocalDB failure is recorded accurately.
- New hosts deliberately offer no P02 business APIs yet. Identity profile/provisioning disagreement in P02 review belongs to the Identity leaf tasks; this scaffold does not reinterpret contracts.
- Per-service database contexts/migrations (P04), bearer policies (P06), frontend (P07), business routes/rules (service leaf issues), aggregate-project removal (D02) and any destructive migration are excluded.
- P02 documents still mark architecture team signoffs pending. The Dispatcher may stage a draft PR with honest evidence; independent human approval is not fabricated by this plan.
