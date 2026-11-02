# GitHub Agent prompt — QuizArena Milestone 3

Copy this entire document into a GitHub-capable agent. This is an issue/board creation assignment. It does not authorize production implementation, branch modification, commits, PR creation, merge, or fabricated approvals during this run. The branch/PR instructions below are requirements to place in the future implementation issues.

## Assignment

In repository **JimRashed/Web-3-Final-Project**, create or reconcile the complete Milestone 3 backlog and publish it on a GitHub Project. Use milestone title **Milestone 3 — Layered Service Development**. Create **1 epic, 8 feature issues, and 54 task issues: 63 total planned records**, minus existing matching issues reused. All records must have a real individual assignee, milestone, feature/parent link, explicit prerequisites, testable acceptance criteria, 3–6 technical checklist items, estimate, review expectations and evidence requirements.

Use the full issue specifications below. Do not replace them with vague titles, abbreviated “implement service” issues or a single flat checklist. Stable IDs such as S03-T02 identify records only until actual GitHub issue numbers are known. Resolve them to actual #numbers and URLs after creation. Never guess issue numbers.

## Confirmed user decisions

- Team: **@JimRashed**, **@CarterBeasse**, **@SuSu-Lisu**.
- Preserve historical ownership: Jim owns **QuestionBank + Lobby**; Carter owns **Identity + Statistics**; Suheng owns **Gameplay + Notification**, including the related frontend vertical slices.
- Use **GitHub Issues + GitHub Projects + GitHub PRs**. The attached source rubric mentions Azure DevOps, but the user explicitly said to use GitHub instead. Record this intentional substitution in submission documentation. Do not create Azure DevOps prerequisite work.
- Frontend: React from the requested MERN-style stack, TypeScript and Node/npm tooling. Persistence remains in the .NET backend with SQL Server. MongoDB is unnecessary. Do not add an Express business API as a second backend.
- This run publishes work tracking only. All architecture/product proposals below require the future Gate 1 review recorded in P02 before production implementation. Publishing them as planned issues is not a claim of design approval.

## Reviewed local baseline and confidence

Research snapshot: 2026-10-30; local HEAD **864d70bd67b2b2b30c170f422c30c5c1c8fe3b11**. Both local main and feature/milestone-3-layered-service-development point to this commit. The milestone branch and origin tracking ref already exist. Origin is https://github.com/JimRashed/Web-3-Final-Project.git; origin/HEAD points to main. Recheck remote state before implementation.

Verified in local files:
- Current solution has **QuizArena.Api, QuizArena.Data, QuizArena.Domain**, one HTTP host, six logical modules, and two aggregate test projects.
- Every repository uses **QuizArenaContext** and one DefaultConnection SQL Server LocalDB database. Older IdentityContext, StatisticsContext, GameplayContext and NotificationContext are not registered runtime service boundaries.
- Application services live in Api. Domain contains HTTP DTOs and application service interfaces; repository interfaces and implementations are both in Data. Domain itself currently has no NuGet dependencies.
- Gameplay uses five hardcoded mock questions, calls Notifications directly in-process, and never automatically records Statistics results. Submitted user/question/state checks are incomplete.
- AuthService collects local credentials and hand-builds HMAC JWTs. Program has UseAuthorization but no configured bearer authentication/UseAuthentication.
- There is no frontend project, layer-specialized agent set or tracked CI workflow in the reviewed file inventory.
- Question entity has prompt/correctAnswer/category but no persisted options. Lobby exposes lock only; there is no usable create/join UI or API. Preferences has PUT only.
- Identity/Statistics use Guid user IDs; Gameplay/Lobby/Notification use strings. MatchResult.MatchId is Guid whereas Gameplay.MatchId is string.
- User.cs and MatchResult.cs are physically under Domain/Lobby/Entities but declare Identity and Statistics namespaces respectively.
- Existing test fixtures use unique SQL Server LocalDB databases and per-test reset. One repository test in UnitTests uses EF InMemory; it is not proof of relational behavior.
- The old review reports **35 passing tests**. That is historical evidence only; this research did not execute build/tests and must not report those counts as current validation.

Read these repository sources before creation and link relevant ones in issue bodies:
1. docs/architecture-notes.md — current modular monolith and bounded contexts.
2. docs/api-service-contracts.md — current 16 routes, DTOs, status codes and cross-module limitations.
3. docs/database-schema.md — unified schema, keys, indexes and mixed identifiers.
4. specs/implementation-plan.md — historical Issue #4 testing plan; do not overwrite it.
5. specs/item-4-database-isolation-plan.md — historical per-test reset plan, not a service-database split plan.
6. docs/code-review-findings.md — historical fixes and deferred LocalDB/lifecycle concerns.
7. docs/milestone2/issue.md — proposed /api/users/me requirement; the route is absent from the current code.
8. docs/reflection.md — existing R-PIV experience.
9. .github/copilot-instructions.md; all six .github/skills/*/SKILL.md and all six .github/agents/*.md — preserve useful workflow but reconcile obsolete paths, constraints and review behavior.
10. The source/code/test paths cited in each service issue below.

Live GitHub issues, collaborator permissions, labels, milestones and Projects were **not verified** in this research environment (no gh executable, no connected GitHub tool). You must check live state to prevent duplicates and verify assignment capability. Local git history alone is not proof of GitHub permissions.

## Proposed implementation contract — put these decisions in P02

These choices fill gaps in the milestone rubric. They deliberately define a minimal coherent product; approval or amendment belongs to P02. Do not silently enlarge scope.

- Six service triples: src/QuizArena.<Service>.Domain, .Data, .Api, with <Service> exactly Identity, Statistics, QuestionBank, Lobby, Gameplay, Notification.
- Unit/integration projects: tests/QuizArena.<Service>.UnitTests and .IntegrationTests; optional tests/QuizArena.TestSupport only for test helpers; tests/QuizArena.ArchitectureTests and tests/QuizArena.EndToEndTests for cross-cutting verification.
- One shared React application at src/QuizArena.Client; service-owned views inside src/features/<service-lowercase>/. Node/npm is frontend tooling. Browser communicates only through HTTP/public DTOs.
- One SQL Server development database per service; existing unified QuizArenaDb remains intact until verified transfer. No cross-service DB queries/foreign keys/project references. Independent runnable services do not require different physical SQL Server instances.
- Domain: pure entities, exceptions, value objects, in-memory calculations/invariants. Data references only its own Domain. Api references its own Data/Domain; HTTP DTOs and application interfaces live in Api. Domain operations must not consume Api DTOs. Api calls peers through typed HTTP adapters and local wire contracts, never peer assemblies. No mandatory shared production “common Domain” library.
- Preserve supported .NET 10/EF 10 packages while separating projects; choose/pin compatible versions during implementation, not arbitrary version upgrades.
- **Keycloak** is the proposed local OIDC authority, subject to P02 approval. React is a public OIDC client with authorization code + PKCE S256. APIs are independent resource servers. The IdP owns sign-in/registration; QuizArena Identity owns player profiles.
- Treat issuer/subject as trusted identity. External subject is opaque string, not assumed Guid. Identity stores unique (Issuer,Subject); public userId is subject in the approved single issuer realm. Use string MatchId across services; Gameplay generates UUID-formatted strings, while historic non-UUID match identifiers remain representable. Question IDs remain Guid.
- Legacy user Guid -> OIDC subject mappings must be explicit; no auto-link based only on an unverified username. Preserve/quarantine unmapped legacy data and old password hashes outside the runtime profile database. No production data destruction.
- Minimal gameplay: exactly two players, five question rounds, each player answers each question once, one point per correct answer, advance when both have answered. Draw means no winner and neither result marks won. No real-time SignalR, matchmaking, email/push transport, catalog CRUD/admin console or message broker is required.
- Persist question snapshots/answers. Gameplay completes atomically with pending HTTP delivery records; a background worker retries idempotent result/event delivery after restart. This is a small local delivery mechanism, not distributed transactions or a broker infrastructure project.
- All current contract changes are intentional and versioned/documented. Retired local password endpoints return 410 with guidance during transition; their clients/tests move to OIDC rather than being silently removed.
- Error responses use ProblemDetails/ValidationProblemDetails: invalid payload400, invalid/missing token401, insufficient scope/ownership403, missing resource404, duplicate/invalid state409, unavailable required dependency503. Unexpected exceptions log sanitized details and return safe500. Do not convert cancellation into generic500.
- Pagination defaults page=1,pageSize=20, pageSize1..100; history newest first with stable key tie-break. Question count1..50; leaderboard top1..100/default10.
- GET /health is deliberately public on each host and does not disclose secrets. Swagger/OpenAPI UI is Development-only; production is disabled or protected. CORS preflight works for configured client origin. Business endpoints are protected unless explicitly retired/public in the contracts.

### Proposed development URL contract

Commit these defaults in P02, keeping all values configurable: React http://localhost:5173; Keycloak http://localhost:8080/realms/quizarena; Identity http://localhost:5101; Statistics http://localhost:5102; QuestionBank http://localhost:5103; Lobby http://localhost:5104; Gameplay http://localhost:5105; Notification http://localhost:5106. HTTP authority metadata is Development-only; deployed environments require HTTPS. Do not let HTTPS redirect configuration silently break the documented local ports. Every Api has a distinct launch profile and its own connection string.

## Exact authorization and endpoint ownership

Policy names below equal their scope strings, with additional resource/service restrictions. Parse space-separated scope or approved scp claims as exact tokens. Authentication alone is insufficient. A signed token with one unrelated scope must fail403. User requests derive subject from validated claims; they may not select the acting user in a request body/query/header.

| API / expected audience | User policies | Machine-only policies | Additional rule |
|---|---|---|---|
| Identity / quizarena-identity | profiles.read, profiles.write | none | /me always resolves validated subject |
| Statistics / quizarena-statistics | stats.read, leaderboard.read | results.write | stats/history self only; leaderboard any authorized player |
| QuestionBank / quizarena-questionbank | questions.read | questions.play | answer keys appear only in internal service response |
| Lobby / quizarena-lobby | lobbies.read, lobbies.write | lobbies.verify | only host locks/starts; two distinct members |
| Gameplay / quizarena-gameplay | matches.read, matches.write | none for public match operations | match reads/submission participant only; creation verifies locked lobby host |
| Notification / quizarena-notification | events.read, preferences.read, preferences.write | events.write | reads/preferences self only |

Configure a confidential Gameplay machine identity with only lobbies.verify, questions.play, results.write and events.write, audiences limited to those downstream resources. It obtains tokens using client credentials, stored server-side through environment secrets. Internal policies require both scope and the approved authenticated machine client identity/claim; a delegated browser/user token must never satisfy that requirement. Use an allowlisted client identity claim configuration appropriate to the provider, documented and tested.

The React public client gets only user scopes above for approved resource audiences and no service credential/internal scopes. Keep access/refresh tokens in memory. Use standard provider discovery/JWKS and framework bearer validation; no manual token verification/decoding, embedded browser secret or password grant. Validate signature, expiry, issuer, audience. Authentication middleware precedes authorization.

Security verification for EACH API: protected route without token401; malformed/bad signature/expired/wrong issuer/wrong audience401; valid missing scope403; valid other scope403; valid required scope success; explicit /health public success. Also test wrong subject/participant and machine-vs-user separation where applicable. Use ephemeral signing keys/local metadata fixture to exercise the real JwtBearer handler. Add a full-stack provider test; do not replace the security matrix with a fake authentication handler.

Official implementation references:
- [ASP.NET Core JWT bearer configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Keycloak JavaScript adapter](https://www.keycloak.org/securing-apps/javascript-adapter)

## GitHub creation procedure (execute now)

1. Read current repository instructions. Use GitHub APIs/tools to inspect all open/closed issues and PRs, collaborators, existing labels, milestones and Projects. Use exact repo owner/name; do not install a different platform.
2. Find/create the milestone by exact title with no invented due date. Find/create Project **QuizArena — Milestone 3** under the repo owner and link the repository. If a tool cannot create Projects, use an available authorized GitHub API; otherwise prepare a truthful export with explicit pending Project setup rather than claim success.
3. Verify all three logins are assignable. Assign each issue to its specified single owner, not to Copilot/bot or all three users. If permissions prevent assignment, preserve the requested assignee in the body, report the failure and flag the work item blocked; do not substitute another person.
4. Create/reuse labels: milestone:m3; type:epic, type:feature, type:task; service:platform, service:identity, service:statistics, service:questionbank, service:lobby, service:gameplay, service:notification, service:delivery; layer:process, layer:architecture, layer:domain, layer:data, layer:api, layer:frontend, layer:security, layer:agents, layer:testing, layer:ci, layer:operations, layer:documentation, layer:review, layer:release; priority:P0 and priority:P1. P0 means prerequisite/security/core requirement, P1 still required for milestone completion. Do not invent deadlines.
5. Use title format **[M3][<stable-id>] <exact title>**. Stable ID plus actual scope defines identity. Search before creation. Preserve meaningful existing issue history; reuse an equivalent issue and map it, or link a historical issue as context. Closed Milestone2 work is not automatically a new Milestone3 task.
6. Create epic first, eight features second, 54 leaf tasks third. Add every record to the milestone and Project. Create native sub-issue relationships Epic -> Feature -> Task where supported. Always include parent/child links in bodies as a portable fallback.
7. Create all bodies using the complete specifications and shared definition of done below. Resolve EVERY stable prerequisite and parent/child reference after all records exist. Use native blocked-by links when supported plus visible prerequisite checklists. Frontend tasks may begin against approved mocks after P02/P07; their live endpoint dependency is a completion blocker, not a reason to prevent parallel UI development.
8. Project fields: Status (Backlog, Ready, In Progress, Blocked, In Review, Done), Priority (P0/P1), Size (S/M/L), Service, Layer, Feature, Milestone. Size planning estimate: S~0.5–1 developer day, M~1–2, L~2–4; these are estimates, not promised dates. Default all new records Backlog; set Ready only if start prerequisites truly met, Blocked for missing inputs/permissions. Never mark work In Progress/Done based only on issue creation.
9. Each body includes: business story; verified current baseline/source files; target behavior; exact owner + suggested independent peer; parent Feature + epic; hard start prerequisites; live/completion prerequisites if different; 3–6 technical checklist subtasks; exact testable acceptance checklist; validation evidence; scope exclusions; three human gates; traceability and commit/PR reference instructions. Expand inherited requirements, do not merely write “see template.”
10. Finish by re-reading all created/reused issues. Verify metadata, real assignments, links, counts, no cycles and no unresolved stable IDs posing as GitHub numbers. Output repository/milestone/Project URLs, a 63-row stable-ID -> actual #number/title/owner/parent/dependencies map, counts created/reused/failed, the critical path and any incomplete permission actions.

## Shared definition of done to include in every task

- Owner is the named human; suggested independent peer rotation: JimRashed -> CarterBeasse, CarterBeasse -> SuSu-Lisu, SuSu-Lisu -> JimRashed. The actual approving reviewer must differ from PR author.
- Gate1: owner reviews research and a file-by-file plan at specs/milestone3/<actual-issue-number>-plan.md; relevant contract changes approved before code. Preserve historical specs/implementation-plan.md rather than overwriting it.
- Gate2: owner inspects diff, runs meaningful tests and checks acceptance criteria/security/secrets. Every changed business rule/application method gets focused unit tests; every changed endpoint gets HTTP integration coverage. Tests accompany layer changes; the final testing task audits coverage rather than postponing all testing.
- Gate3: read-only agent severity report is inspected, critical/high findings resolved and at least one other human approves the PR with dated signoff and CI links. Agents never fabricate human approval or auto-merge.
- Controllers stay thin; Domain contains pure rules; Data owns EF; Api owns public DTOs/orchestration. Constructor DI, async end to end and propagated CancellationToken. No .Result/.Wait or entity leakage.
- Unit tests use xUnit + FluentAssertions + Moq, Arrange/Act/Assert, MethodName_Scenario_ExpectedBehavior. No real DB in unit tests. SQL Server integration tests own unique generated databases, reset per test and clean up safely. No dependency on test order and no skip/delete tests to force green.
- Every code PR references its issue; parent feature/epic links appear in PR body. Include accepted plan, summary, actual validation commands/results, security evidence and human gate log.
- No credentials/tokens, generated binaries, node_modules or coverage files committed. Versions/lockfiles and configuration examples are explicit; secrets use environment/user-secrets/CI secret storage.
- All applicable checks pass with zero warnings/errors and zero failed/skipped tests. Non-code issues use their specified evidence checks and actual linked CI where relevant rather than inventing tests.
- No issue closes solely because files exist. Its applicable backend/frontend acceptance and human review evidence must be attached.

### Validation command contract

From repository root after the split:
```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal
dotnet test QuizArena.sln --no-build --collect:"XPlat Code Coverage"
```
Run focused service tests using the corresponding per-service .csproj paths. The initial scaffolding must make those projects discoverable. Do not hardcode the historical test count as an acceptance target.

From src/QuizArena.Client:
```text
npm ci
npm run typecheck
npm run lint
npm run test -- --run
npm run build
npm run test:e2e
```
Define those scripts in P07/P10/D03. Relational tests require provisioned SQL Server/LocalDB; local OIDC smoke/E2E requires the configured provider. CI must supply prerequisites, not silently skip.

## Milestone branch and PR rules to copy into P01/D07

Usual integration branch is main (verified locally; reconfirm remotely). Milestone branch is feature/milestone-3-layered-service-development. If absent, create from updated main with git switch -c feature/milestone-3-layered-service-development. If present, reuse it after ancestry inspection; never recreate/reset it. Task branches such as feature/m3/<issue>-<slug> branch from the milestone branch and task PRs target that milestone branch. Only reviewed task changes enter it. If parent implementation across multiple layer tasks uses one PR, reference every contributing task and do not close incomplete tasks early.

On the first milestone push use git push -u origin feature/milestone-3-layered-service-development. Final milestone-level **GitHub** PR targets main; include linked epic/features/tasks, “Milestone 3 — Layered Service Development,” change summary, validation results and human review signoff. Keep the milestone branch after merge. Do not push directly to main. Prepare the merged milestone GitHub PR link for Lea; do not claim course submission without a real human action.

## Issue specifications

### E00 — Complete Milestone 3: Layered Service Development

**Type:** Epic. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Priority:** P0. **Size:** L (roll-up). **Milestone:** Milestone 3 — Layered Service Development. **Prerequisites:** none for tracking; implementation gates below.

**User story:** As the QuizArena team, we need six independently runnable layered services, a connected React frontend, standard OAuth/OIDC security, traceable work and verified human review so we can demonstrate the required service-oriented foundation.

**Technical coordination subtasks:**
- [ ] Approve contracts, ownership, project/data boundaries and OIDC decisions before parallel production work.
- [ ] Deliver all six vertical slices through Domain, Data, application, endpoints, frontend and tests.
- [ ] Validate security, migration, complete browser journeys and repeatable local/CI execution.
- [ ] Publish architecture/reflection, traceable board, actual peer signoffs and merged retained-branch milestone PR.

**Acceptance criteria:**
- [ ] Each of three confirmed members owns at least two independent service hosts with dedicated Domain/Data/Api projects and connected frontend views.
- [ ] All eight child features and 54 tasks meet their specific acceptance and human gates.
- [ ] Every API uses real bearer validation and least-privilege policies with positive and negative automated evidence.
- [ ] Board, agents/skills, plans, tests, docs and PR evidence meet every attached rubric section using the user's GitHub substitution.
- [ ] Six-service solution/frontend build cleanly; all tests pass; milestone branch retained after merge.

**Child features:** F00, F01, F02, F03, F04, F05, F06, F07. Resolve to real links. **Closure evidence:** D08 submission bundle plus native child completion. No production implementation is authorized by backlog publication.

### F00 — Establish contracts, architecture, security, frontend foundations and agent workflow

**Type:** Feature. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent:** E00. **Priority:** P0. **Size:** L (roll-up). **Service:** platform. **Prerequisites:** none to publish; children define implementation start gates.

**User story:** As a team, we can develop independently against agreed secure contracts and consistent tools.

**Technical coordination subtasks:**
- [ ] Confirm child issue specifications, agreed contracts and file ownership; approve the feature's plan.
- [ ] Deliver the linked layer/foundation tasks with focused tests and work-item references.
- [ ] Verify applicable backend and frontend acceptance, negative security paths and persisted behavior.
- [ ] Collect real CI, read-only review and independent human signoff evidence before closure.

**Acceptance criteria:**
- [ ] All P01–P10 tasks are accepted; six hosts and one React client scaffold exist, contracts are approved, real bearer tests and CI work; foundations enable parallel service ownership.
- [ ] Every linked child issue is accepted with its own validation evidence and applicable human gates.
- [ ] Owner and independent peer verify requirements coverage; do not treat an automated agent report as approval.

**Linked child task checklist:**
- [ ] P01 — Publish the milestone board, ownership map and GitHub delivery rules (@JimRashed)
- [ ] P02 — Approve service boundaries, public contracts, identity mapping and security policies (@JimRashed)
- [ ] P03 — Scaffold six independently runnable layered services and migrate the solution graph (@JimRashed)
- [ ] P04 — Establish service-owned EF configuration, migrations and safe database boundaries (@JimRashed)
- [ ] P05 — Configure a reproducible OIDC provider and client/service identities (@CarterBeasse)
- [ ] P06 — Apply real JWT validation and reusable scope policies to all six APIs (@CarterBeasse)
- [ ] P07 — Build the shared React frontend shell, OIDC session and HTTP clients (@JimRashed)
- [ ] P08 — Add constrained layer agents and reconcile the repository skills with Milestone 3 (@JimRashed)
- [ ] P09 — Create isolated per-service test hosts and a real bearer-token test fixture (@SuSu-Lisu)
- [ ] P10 — Add CI gates for architecture, backend, frontend and security tests (@SuSu-Lisu)

**Validation/evidence:** child task results, PRs, feature live demonstration where applicable, and requirements traceability. **Out of scope:** behavior beyond the approved P02 contract. Resolve parent/child/prerequisite IDs to actual issue links after creation.

### F01 — Deliver the independently runnable Identity service and connected React views

**Type:** Feature. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent:** E00. **Priority:** P0. **Size:** L (roll-up). **Service:** Identity. **Prerequisites:** P02, P03.

**User story:** As a signed-in player, I can see my own provider-backed profile without giving my identity-provider password to QuizArena.

**Technical coordination subtasks:**
- [ ] Confirm child issue specifications, agreed contracts and file ownership; approve the feature's plan.
- [ ] Deliver the linked layer/foundation tasks with focused tests and work-item references.
- [ ] Verify applicable backend and frontend acceptance, negative security paths and persisted behavior.
- [ ] Collect real CI, read-only review and independent human signoff evidence before closure.

**Acceptance criteria:**
- [ ] Six layer tasks completed with Domain/Data/Api separation, all specified REST and UI behavior, real OAuth bearer policies, persistence and negative tests. Identity runs independently and uses no other service assemblies or tables. GET /api/users/me => 200 profile, profiles.read; PUT /api/users/me => 200 profile, profiles.write, request {displayName:string}. Retire POST /api/auth/register and /api/auth/login with 410 ProblemDetails and documented OIDC redirect guidance; do not accept password payloads or mint tokens. GET /api/users/{id} is replaced by /me and returns 410 during documented transition. Frontend: ProfilePage.tsx: provider sign-in/sign-out buttons, current profile, display-name edit form; show 401 sign-in action, 403 permission message, field validation and saving state. No QuizArena password form.
- [ ] Every linked child issue is accepted with its own validation evidence and applicable human gates.
- [ ] Owner and independent peer verify requirements coverage; do not treat an automated agent report as approval.

**Linked child task checklist:**
- [ ] S01-T01 — Implement Identity domain models and pure business rules (@CarterBeasse)
- [ ] S01-T02 — Implement Identity EF mappings, migrations and repositories (@CarterBeasse)
- [ ] S01-T03 — Implement Identity API DTOs and application orchestration (@CarterBeasse)
- [ ] S01-T04 — Expose and secure Identity REST endpoints with OpenAPI (@CarterBeasse)
- [ ] S01-T05 — Build Identity React screens and REST integration (@CarterBeasse)
- [ ] S01-T06 — Complete Identity vertical-slice, authorization and regression coverage (@CarterBeasse)

**Validation/evidence:** child task results, PRs, feature live demonstration where applicable, and requirements traceability. **Out of scope:** behavior beyond the approved P02 contract. Resolve parent/child/prerequisite IDs to actual issue links after creation.

### F02 — Deliver the independently runnable Statistics service and connected React views

**Type:** Feature. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent:** E00. **Priority:** P0. **Size:** L (roll-up). **Service:** Statistics. **Prerequisites:** P02, P03.

**User story:** As a player, I can view my match history, totals and leaderboard, and only trusted gameplay can submit results.

**Technical coordination subtasks:**
- [ ] Confirm child issue specifications, agreed contracts and file ownership; approve the feature's plan.
- [ ] Deliver the linked layer/foundation tasks with focused tests and work-item references.
- [ ] Verify applicable backend and frontend acceptance, negative security paths and persisted behavior.
- [ ] Collect real CI, read-only review and independent human signoff evidence before closure.

**Acceptance criteria:**
- [ ] Six layer tasks completed with Domain/Data/Api separation, all specified REST and UI behavior, real OAuth bearer policies, persistence and negative tests. Statistics runs independently and uses no other service assemblies or tables. POST /api/results => 200 empty on first or identical retry, results.write service-only; conflicting duplicate =>409. GET /api/players/{userId}/stats =>200, stats.read and self; GET /api/players/{userId}/matches?page=1&pageSize=20 =>200, stats.read and self; invalid page/pageSize (1..100)=>400. GET /api/leaderboard?top=10 =>200, leaderboard.read, all signed-in players. Other user's self-only stats/history=>403. Frontend: StatisticsPage.tsx and LeaderboardPage.tsx: current player's totals, paged newest-first history, leaderboard top selector; empty-state text, loading/error/retry, no result-submission form.
- [ ] Every linked child issue is accepted with its own validation evidence and applicable human gates.
- [ ] Owner and independent peer verify requirements coverage; do not treat an automated agent report as approval.

**Linked child task checklist:**
- [ ] S02-T01 — Implement Statistics domain models and pure business rules (@CarterBeasse)
- [ ] S02-T02 — Implement Statistics EF mappings, migrations and repositories (@CarterBeasse)
- [ ] S02-T03 — Implement Statistics API DTOs and application orchestration (@CarterBeasse)
- [ ] S02-T04 — Expose and secure Statistics REST endpoints with OpenAPI (@CarterBeasse)
- [ ] S02-T05 — Build Statistics React screens and REST integration (@CarterBeasse)
- [ ] S02-T06 — Complete Statistics vertical-slice, authorization and regression coverage (@CarterBeasse)

**Validation/evidence:** child task results, PRs, feature live demonstration where applicable, and requirements traceability. **Out of scope:** behavior beyond the approved P02 contract. Resolve parent/child/prerequisite IDs to actual issue links after creation.

### F03 — Deliver the independently runnable QuestionBank service and connected React views

**Type:** Feature. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent:** E00. **Priority:** P0. **Size:** L (roll-up). **Service:** QuestionBank. **Prerequisites:** P02, P03.

**User story:** As a player, I can browse available quiz content, while Gameplay securely obtains the answer material needed for a real persisted match.

**Technical coordination subtasks:**
- [ ] Confirm child issue specifications, agreed contracts and file ownership; approve the feature's plan.
- [ ] Deliver the linked layer/foundation tasks with focused tests and work-item references.
- [ ] Verify applicable backend and frontend acceptance, negative security paths and persisted behavior.
- [ ] Collect real CI, read-only review and independent human signoff evidence before closure.

**Acceptance criteria:**
- [ ] Six layer tasks completed with Domain/Data/Api separation, all specified REST and UI behavior, real OAuth bearer policies, persistence and negative tests. QuestionBank runs independently and uses no other service assemblies or tables. GET /api/questions/generate?count=10 =>200, questions.read; count outside 1..50=>400; empty catalog=>200 []. POST /internal/question-sets {count:5} =>200 exact immutable snapshot, questions.play service-only; insufficient valid catalog=>409. No public DTO or Swagger browser sample exposes correct answers. Frontend: QuestionBankPage.tsx: question-count selector, generated question preview with options/category, regenerate button, empty/loading/error/validation states; never render correct answers or call /internal/question-sets.
- [ ] Every linked child issue is accepted with its own validation evidence and applicable human gates.
- [ ] Owner and independent peer verify requirements coverage; do not treat an automated agent report as approval.

**Linked child task checklist:**
- [ ] S03-T01 — Implement QuestionBank domain models and pure business rules (@JimRashed)
- [ ] S03-T02 — Implement QuestionBank EF mappings, migrations and repositories (@JimRashed)
- [ ] S03-T03 — Implement QuestionBank API DTOs and application orchestration (@JimRashed)
- [ ] S03-T04 — Expose and secure QuestionBank REST endpoints with OpenAPI (@JimRashed)
- [ ] S03-T05 — Build QuestionBank React screens and REST integration (@JimRashed)
- [ ] S03-T06 — Complete QuestionBank vertical-slice, authorization and regression coverage (@JimRashed)

**Validation/evidence:** child task results, PRs, feature live demonstration where applicable, and requirements traceability. **Out of scope:** behavior beyond the approved P02 contract. Resolve parent/child/prerequisite IDs to actual issue links after creation.

### F04 — Deliver the independently runnable Lobby service and connected React views

**Type:** Feature. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent:** E00. **Priority:** P0. **Size:** L (roll-up). **Service:** Lobby. **Prerequisites:** P02, P03.

**User story:** As a player, I can create or join a two-player lobby and, as its host, lock it before starting a match.

**Technical coordination subtasks:**
- [ ] Confirm child issue specifications, agreed contracts and file ownership; approve the feature's plan.
- [ ] Deliver the linked layer/foundation tasks with focused tests and work-item references.
- [ ] Verify applicable backend and frontend acceptance, negative security paths and persisted behavior.
- [ ] Collect real CI, read-only review and independent human signoff evidence before closure.

**Acceptance criteria:**
- [ ] Six layer tasks completed with Domain/Data/Api separation, all specified REST and UI behavior, real OAuth bearer policies, persistence and negative tests. Lobby runs independently and uses no other service assemblies or tables. POST /api/lobbies {} =>201, lobbies.write, Location points to GET; GET /api/lobbies/{joinCode} =>200, lobbies.read, sanitized join preview; POST /api/lobbies/{joinCode}/join {} =>200, lobbies.write; POST /api/lobbies/{joinCode}/lock =>200, lobbies.write plus host rule. Missing=>404; invalid joinCode=>400; invalid state/capacity=>409; nonhost lock=>403. GET /internal/lobbies/{joinCode} =>200, lobbies.verify service-only. Frontend: LobbyPage.tsx: create/join forms, join code display/copy, host/player membership and lock state; host-only lock/start controls, polling and cleanup; Start invokes Gameplay create after lock, displays navigation to returned match.
- [ ] Every linked child issue is accepted with its own validation evidence and applicable human gates.
- [ ] Owner and independent peer verify requirements coverage; do not treat an automated agent report as approval.

**Linked child task checklist:**
- [ ] S04-T01 — Implement Lobby domain models and pure business rules (@JimRashed)
- [ ] S04-T02 — Implement Lobby EF mappings, migrations and repositories (@JimRashed)
- [ ] S04-T03 — Implement Lobby API DTOs and application orchestration (@JimRashed)
- [ ] S04-T04 — Expose and secure Lobby REST endpoints with OpenAPI (@JimRashed)
- [ ] S04-T05 — Build Lobby React screens and REST integration (@JimRashed)
- [ ] S04-T06 — Complete Lobby vertical-slice, authorization and regression coverage (@JimRashed)

**Validation/evidence:** child task results, PRs, feature live demonstration where applicable, and requirements traceability. **Out of scope:** behavior beyond the approved P02 contract. Resolve parent/child/prerequisite IDs to actual issue links after creation.

### F05 — Deliver the independently runnable Gameplay service and connected React views

**Type:** Feature. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent:** E00. **Priority:** P0. **Size:** L (roll-up). **Service:** Gameplay. **Prerequisites:** P02, P03.

**User story:** As one of two lobby players, I can complete a fair five-round persisted quiz match and see a result that reaches Statistics and Notifications.

**Technical coordination subtasks:**
- [ ] Confirm child issue specifications, agreed contracts and file ownership; approve the feature's plan.
- [ ] Deliver the linked layer/foundation tasks with focused tests and work-item references.
- [ ] Verify applicable backend and frontend acceptance, negative security paths and persisted behavior.
- [ ] Collect real CI, read-only review and independent human signoff evidence before closure.

**Acceptance criteria:**
- [ ] Six layer tasks completed with Domain/Data/Api separation, all specified REST and UI behavior, real OAuth bearer policies, persistence and negative tests. Gameplay runs independently and uses no other service assemblies or tables. POST /matches {joinCode} =>201, matches.write and lobby host; repeat same lobby=>200 same match. GET /matches/{matchId} =>200, matches.read and participant; GET /matches/{matchId}/next-question =>200 public question, matches.read and participant; finished=>409. POST /matches/{matchId}/answers {questionId:Guid,answer:string} =>200 {correct,playerAScore,playerBScore,roundComplete,finished}, matches.write and participant. Missing=>404; invalid input=>400; replay/stale/finished=>409; dependency unavailable before creation=>503 without partial match. Frontend: MatchPage.tsx: current public question/options, answer submit once, waiting-for-opponent display with polling, live scores, final winner/draw and links to stats/events. Hide answer key; disable duplicate submission and handle 409 refresh, 401/403 and network retry.
- [ ] Every linked child issue is accepted with its own validation evidence and applicable human gates.
- [ ] Owner and independent peer verify requirements coverage; do not treat an automated agent report as approval.

**Linked child task checklist:**
- [ ] S05-T01 — Implement Gameplay domain models and pure business rules (@SuSu-Lisu)
- [ ] S05-T02 — Implement Gameplay EF mappings, migrations and repositories (@SuSu-Lisu)
- [ ] S05-T03 — Implement Gameplay API DTOs and application orchestration (@SuSu-Lisu)
- [ ] S05-T04 — Expose and secure Gameplay REST endpoints with OpenAPI (@SuSu-Lisu)
- [ ] S05-T05 — Build Gameplay React screens and REST integration (@SuSu-Lisu)
- [ ] S05-T06 — Complete Gameplay vertical-slice, authorization and regression coverage (@SuSu-Lisu)

**Validation/evidence:** child task results, PRs, feature live demonstration where applicable, and requirements traceability. **Out of scope:** behavior beyond the approved P02 contract. Resolve parent/child/prerequisite IDs to actual issue links after creation.

### F06 — Deliver the independently runnable Notification service and connected React views

**Type:** Feature. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent:** E00. **Priority:** P0. **Size:** L (roll-up). **Service:** Notification. **Prerequisites:** P02, P03.

**User story:** As a player, I can read my match events and manage my notification preferences without exposing another player's data.

**Technical coordination subtasks:**
- [ ] Confirm child issue specifications, agreed contracts and file ownership; approve the feature's plan.
- [ ] Deliver the linked layer/foundation tasks with focused tests and work-item references.
- [ ] Verify applicable backend and frontend acceptance, negative security paths and persisted behavior.
- [ ] Collect real CI, read-only review and independent human signoff evidence before closure.

**Acceptance criteria:**
- [ ] Six layer tasks completed with Domain/Data/Api separation, all specified REST and UI behavior, real OAuth bearer policies, persistence and negative tests. Notification runs independently and uses no other service assemblies or tables. POST /events {eventId:Guid,type,userId,payload:{matchId,winnerUserId?,draw?}} =>202 persisted EventResponse, events.write service-only; exact retry same response, conflict409. GET /events?page=1&pageSize=20 =>200 self events, events.read; legacy userId query if supplied must equal caller or403. GET /users/{userId}/preferences =>200 defaults/stored, preferences.read and self; PUT same route {emailEnabled,pushEnabled} =>200, preferences.write and self. Invalid payload/page=>400; cross-user=>403. Frontend: NotificationsPage.tsx: self event timeline with pagination/empty state and PreferencesPage.tsx with email/push toggles, save feedback and failures; explain that external delivery is not implemented, show only actual event records.
- [ ] Every linked child issue is accepted with its own validation evidence and applicable human gates.
- [ ] Owner and independent peer verify requirements coverage; do not treat an automated agent report as approval.

**Linked child task checklist:**
- [ ] S06-T01 — Implement Notification domain models and pure business rules (@SuSu-Lisu)
- [ ] S06-T02 — Implement Notification EF mappings, migrations and repositories (@SuSu-Lisu)
- [ ] S06-T03 — Implement Notification API DTOs and application orchestration (@SuSu-Lisu)
- [ ] S06-T04 — Expose and secure Notification REST endpoints with OpenAPI (@SuSu-Lisu)
- [ ] S06-T05 — Build Notification React screens and REST integration (@SuSu-Lisu)
- [ ] S06-T06 — Complete Notification vertical-slice, authorization and regression coverage (@SuSu-Lisu)

**Validation/evidence:** child task results, PRs, feature live demonstration where applicable, and requirements traceability. **Out of scope:** behavior beyond the approved P02 contract. Resolve parent/child/prerequisite IDs to actual issue links after creation.

### F07 — Integrate, validate, document and submit the reviewed GitHub milestone

**Type:** Feature. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent:** E00. **Priority:** P0. **Size:** L (roll-up). **Service:** delivery. **Prerequisites:** F00, F01, F02, F03, F04, F05, F06.

**User story:** As the team, we can demonstrate and submit a working, secure, tested multi-service product with real review evidence.

**Technical coordination subtasks:**
- [ ] Confirm child issue specifications, agreed contracts and file ownership; approve the feature's plan.
- [ ] Deliver the linked layer/foundation tasks with focused tests and work-item references.
- [ ] Verify applicable backend and frontend acceptance, negative security paths and persisted behavior.
- [ ] Collect real CI, read-only review and independent human signoff evidence before closure.

**Acceptance criteria:**
- [ ] D01–D08 accepted: clean startup, safe data cutover, complete browser E2E, 2–3-page reflection, traceable published board, actual peer signoff, merged milestone GitHub PR and retained milestone branch.
- [ ] Every linked child issue is accepted with its own validation evidence and applicable human gates.
- [ ] Owner and independent peer verify requirements coverage; do not treat an automated agent report as approval.

**Linked child task checklist:**
- [ ] D01 — Run the six-service application and frontend from a documented local environment (@JimRashed)
- [ ] D02 — Migrate legacy data safely and retire the aggregate host and shared context (@JimRashed)
- [ ] D03 — Verify the complete secured two-player journey and delivery retries (@SuSu-Lisu)
- [ ] D04 — Write the Milestone 3 architecture, security and reflection report (@CarterBeasse)
- [ ] D05 — Audit issue traceability, acceptance evidence and board completeness (@JimRashed)
- [ ] D06 — Complete read-only review and human signoff for the milestone candidate (@SuSu-Lisu)
- [ ] D07 — Merge the retained milestone branch through a reviewed GitHub milestone PR (@JimRashed)
- [ ] D08 — Publish the final GitHub milestone submission bundle and board export (@JimRashed)

**Validation/evidence:** child task results, PRs, feature live demonstration where applicable, and requirements traceability. **Out of scope:** behavior beyond the approved P02 contract. Resolve parent/child/prerequisite IDs to actual issue links after creation.

## Complete task bodies

The full common definition of done is incorporated into every body below. When publishing, append that definition to each issue so a developer can act from the issue alone. Retain these specific requirements in full.

### P01 — Publish the milestone board, ownership map and GitHub delivery rules

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** process. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** None; begin with live repository/board inventory.

**User story:** As a team, we need accountable work tracking and a safe milestone integration path before production implementation.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** docs/milestone3/work-allocation.md; docs/milestone3/workflow.md; .github/pull_request_template.md; GitHub Project and milestone metadata

**Technical subtasks:**
- [ ] Verify collaborator logins/permissions, existing issues/PRs/milestones and main as the integration branch; link/reuse matching work items.
- [ ] Record Jim=QuestionBank/Lobby, Carter=Identity/Statistics, Suheng=Gameplay/Notification with peer-review rotation; publish the board and dependency map.
- [ ] Document milestone branch, task branch/PR targets, retained milestone branch, issue references, three human gates and GitHub submission URLs.

**Specific acceptance criteria:**
- [ ] Board includes epic, all features/tasks, owners, milestones and dependencies; each member owns two runnable services and related UI. Current feature/milestone-3-layered-service-development already exists locally and on origin and currently matches main; verify ancestry before reuse, never recreate/reset it. New work only enters main through reviewed milestone PR. Source rubric mentions Azure DevOps/Lea; user explicitly selected GitHub, so use GitHub equivalents and record this override rather than provisioning Azure DevOps.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Inspect board URLs, owner fields, hierarchy, branch ancestry, template and traceability rules. Do not claim peer approval before a real human review.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### P02 — Approve service boundaries, public contracts, identity mapping and security policies

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** architecture. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P01

**User story:** As parallel implementers, we need committed contracts and explicit behavior so service and frontend changes agree.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** docs/milestone3/architecture-decisions.md; docs/milestone3/api-contracts.md; docs/milestone3/authorization-matrix.md; docs/milestone3/identity-migration.md; contracts/openapi/*.yaml

**Technical subtasks:**
- [ ] Review and approve every proposed architecture/product decision in this prompt, especially Keycloak, opaque subject/string match IDs, two-player/five-round rules and added endpoint scope.
- [ ] Commit schemas, endpoint/status/policy matrix, service/client base URLs, error schema, pagination and idempotency rules before implementation fans out.
- [ ] Define account mapping from legacy Guid IDs to (issuer,subject), machine identities, allowed scopes/audiences, match/event delivery contracts and version transition; collect all three members' approval.

**Specific acceptance criteria:**
- [ ] Six owners' service contracts (two per member) and frontend routes are explicit; no public answer keys or client-selected current-user IDs. API DTOs and application interfaces live in each Api; Domain contains pure entities/rules. No service shares another service's assemblies or database tables. Changes to existing endpoints/IDs and account/password migration are documented. Architecture decisions are proposals until Gate 1 approval; issue creation itself does not approve them.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Contract review with all three members; compare OpenAPI with all leaf issue scopes, including positive/negative examples; every route has public/protected status and required named policy.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### P03 — Scaffold six independently runnable layered services and migrate the solution graph

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** architecture. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P02

**User story:** As a developer, I need independent service hosts and enforced project boundaries.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** QuizArena.sln; src/QuizArena.{Identity,Statistics,QuestionBank,Lobby,Gameplay,Notification}.{Domain,Data,Api}/*.csproj; each Api/Program.cs and Properties/launchSettings.json; tests/QuizArena.ArchitectureTests/

**Technical subtasks:**
- [ ] Create 18 net10.0 backend projects and six pairs of per-service test projects; add to solution with unique host entrypoint types/namespaces.
- [ ] Create composition roots, configurable ports and unauthenticated GET /health endpoints; migrate code incrementally into the owned projects without duplicating runtime ownership.
- [ ] Add architecture tests for Domain with zero NuGet/outer/web dependencies, Data->own Domain only, Api->own Data/Domain only, no cross-service project references; coordinate removal of obsolete aggregate projects in D02.

**Specific acceptance criteria:**
- [ ] All six hosts build and each launches with its own configuration/port even when peers are down; /health verifies liveness, not downstream dependencies. Dedicated Domain/Data/Api projects exist for every service; a mere namespace split or six contexts in one API is insufficient. Pure services do not depend on HTTP DTOs. No frontend backend-assembly references. Architecture tests fail for prohibited references.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** dotnet build QuizArena.sln --no-restore -warnaserror; run architecture tests; launch each Api and request /health with other APIs stopped.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### P04 — Establish service-owned EF configuration, migrations and safe database boundaries

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** data. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P02; [ ] P03

**User story:** As a service owner, I need independently managed persistence without accidental cross-module table access.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** each src/QuizArena.<Service>.Data/{Context,Configurations,Migrations}/; each Api connection-string configuration; docs/milestone3/database-operations.md; .config/dotnet-tools.json

**Technical subtasks:**
- [ ] Use SQL Server provider with six development databases QuizArena_<Service>; each service connects only to its own database and owns its migrations/history.
- [ ] Extract unified context mappings into owned IEntityTypeConfiguration classes; preserve documented indexes, required columns and intradomain cascade/restrict rules; add design-time factories.
- [ ] Document explicit per-context migrate commands, startup/readiness, unique test databases, credentials from environment/secrets and non-destructive old-database retention. Leave actual data transfer to D02.

**Specific acceptance criteria:**
- [ ] No cross-service foreign keys, navigation properties or shared context survive final cutover; legacy QuizArenaDb is preserved during migration. Six migration lineages apply to empty databases. No schema is initialized in production with EnsureCreated. Connection credentials are never committed. Service-specific schema constraints are completed by the corresponding data task.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** For each service: dotnet ef database update --project src/QuizArena.<Service>.Data --startup-project src/QuizArena.<Service>.Api --context <Service>DbContext; inspect owned tables and relational constraints.

**Agent / workflow:** Data & Persistence Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### P05 — Configure a reproducible OIDC provider and client/service identities

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** security. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P02

**User story:** As a user and service caller, I need standard OAuth/OIDC tokens with intentional access grants.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** infra/oidc/; infra/compose.yaml; docs/milestone3/authentication.md; .env.example; src/QuizArena.Client/src/auth/

**Technical subtasks:**
- [ ] After approval of Keycloak in P02, configure realm quizarena, public quizarena-client using authorization code + PKCE S256 and exact redirect/logout origins.
- [ ] Register resource audiences and least-privilege service identities/scopes from the matrix; configure player scope grants without exposing internal scopes.
- [ ] Provide reproducible local startup/realm setup, provision users/secrets outside Git, disable password grant/implicit flow and document client/resource owner/authorization server/resource servers.

**Specific acceptance criteria:**
- [ ] Browser has no client secret and never sends an IdP password to QuizArena. Gameplay confidential client may request lobbies.verify/questions.play/results.write/events.write with only corresponding audiences; browser gets none. Test fixture keys are ephemeral; production signing credentials absent. Approved issuer/audience/HTTPS settings vary by environment; development HTTP relaxation limited to Development.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Manually demonstrate provider login/PKCE redirect, minted access-token audience/scopes and negative insufficient-grant cases; document metadata/JWKS locations and setup without recording tokens or secrets.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### P06 — Apply real JWT validation and reusable scope policies to all six APIs

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** security. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P03; [ ] P05

**User story:** As a resource owner, I need every API to validate tokens and enforce least privilege independently.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** each Api/{Program.cs,Security/,appsettings.json}; docs/milestone3/authorization-matrix.md; each IntegrationTests/Security/

**Technical subtasks:**
- [ ] Use Microsoft.AspNetCore.Authentication.JwtBearer with configured authority/audience; validate signature, issuer, audience, lifetime and signing keys; UseAuthentication before UseAuthorization.
- [ ] Implement exact scope membership for space-delimited scope and supported scp claims; apply explicit named policies and service-principal requirements. Use validated sub/iss for user ownership.
- [ ] Set authenticated fallback policy; explicitly allow /health and Development-only documentation; configure scoped CORS for approved frontend origin and safe ProblemDetails responses.

**Specific acceptance criteria:**
- [ ] Each host rejects missing/invalid/expired/wrong-issuer/wrong-audience tokens with401, valid insufficient/wrong scope with403, valid required scope succeeds; ID tokens are not accepted as access tokens. Browser tokens cannot call internal ingestion even if a test token includes a scope without service identity. Scope substring matches never authorize. Public /health works without token. Ownership checks supplement scopes. No mock auth replaces real bearer validation in the security suite.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Per-service parameterized security matrix using ephemeral signing keys and local metadata fixture, including bad signature, expired token, unknown key, wrong issuer/audience, absent/wrong scope, authorized scope, public route and spoofed identity.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### P07 — Build the shared React frontend shell, OIDC session and HTTP clients

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** frontend. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P02; [ ] P05

**User story:** As a player, I need one connected browser client for all six services.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** src/QuizArena.Client/{package.json,package-lock.json,vite.config.ts,src/app/,src/auth/,src/api/,src/components/}; docs/milestone3/frontend-setup.md

**Technical subtasks:**
- [ ] Scaffold React+TypeScript with Node/npm tooling, routing and navigation for profile, questions, lobby, match, statistics, leaderboard, events and preferences.
- [ ] Implement PKCE provider login/logout/session refresh and bearer HTTP clients with independently configurable base URLs for six APIs; memory-only tokens, no browser client secret.
- [ ] Provide reusable field validation, loading/empty/error/retry/401/403 displays, abortable requests and component test setup. Screens themselves belong to feature frontend tasks.

**Specific acceptance criteria:**
- [ ] Use React from the requested MERN-style stack; .NET APIs own all persistence, no MongoDB is required. Node/npm supplies frontend tooling; no second Express business API or direct SQL access. Browser communicates only by REST and public DTOs. Distinct service ports work with CORS. Logout clears client state. Refresh failure returns user to sign-in without infinite retry.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** npm ci; npm run typecheck; npm run lint; npm run build; npm run test -- --run in src/QuizArena.Client; test auth redirect, bearer attachment, refresh failure and common error components.

**Agent / workflow:** Frontend Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### P08 — Add constrained layer agents and reconcile the repository skills with Milestone 3

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** agents. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P02

**User story:** As an AI-assisted developer, I need agents that respect service/layer boundaries and human gates.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** .github/agents/{domain-dev,data-persistence,api-service,frontend-dev,testing,code-review}.agent.md; .github/skills/{domain-dev,data-persistence,api-service,frontend-dev,testing,code-review,prime,plan,implement,validate}/SKILL.md; .github/copilot-instructions.md

**Technical subtasks:**
- [ ] Create valid agent frontmatter, real supported tool allowlists, user-invocable names and service-scoped context; Domain/Data/Api/Client writing boundaries are explicit.
- [ ] Update tests to per-service paths and Moq/xUnit/FluentAssertions; relational persistence coverage uses isolated SQL Server, not EF InMemory as constraint evidence.
- [ ] Make review agent read-only; remove nonexistent skill tool names, stale aggregate layout and instruction to patch during read-only review; use specs/milestone3/<issue>-plan.md and three real human gates.

**Specific acceptance criteria:**
- [ ] All four layer agents plus testing/review agents exist with linked readable skills; at least two layer agents and one testing/review skill satisfy the rubric. Domain agent cannot add EF/Web dependencies; Data cannot reference Api; frontend cannot reference backend assemblies; testing agent runs baseline before changes and edits tests only. Cross-layer changes are handed to the appropriate agent. Review reports severity but cannot edit/approve/merge. Existing useful workflow is retained with consistent paths.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Inspect frontmatter/tool validity and dry-run one bounded research/plan example per role; verify file access constraints and absence of contradictory test paths/providers.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### P09 — Create isolated per-service test hosts and a real bearer-token test fixture

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** testing. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P03; [ ] P04; [ ] P06

**User story:** As a developer, I need deterministic tests that catch relational and security regressions.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** tests/QuizArena.<Service>.{UnitTests,IntegrationTests}/; tests/QuizArena.TestSupport/; tests/QuizArena.ArchitectureTests/; docs/milestone3/testing.md

**Technical subtasks:**
- [ ] Migrate current service tests to their owned projects, preserve scenario coverage and record an actual baseline rather than copying historical 35-test results.
- [ ] Build service-specific WebApplicationFactory hosts, unique SQL Server databases, per-test reset/seeding and reliable cleanup; mock outbound HTTP for focused integration tests.
- [ ] Generate ephemeral signed test JWTs and local discovery/JWKS metadata for real JwtBearer; support service/user identities and negative token permutations without bypassing production policies.

**Specific acceptance criteria:**
- [ ] Unit tests use Moq and no actual database; relational tests use the SQL Server provider and verify constraints. No test touches developer databases or depends on order. Existing fixture hidden DisposeAsync concern is resolved using deliberate lifecycle ownership. Framework InMemory tests are moved/reclassified when they exercise repositories. Every service has separate test results; new feature tests are added in feature test tasks.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run all six unit/integration projects independently and together; verify repeated execution starts clean; run security fixture negative controls; cleanup removes only generated test databases.

**Agent / workflow:** Testing Agent. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### P10 — Add CI gates for architecture, backend, frontend and security tests

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F00. **Epic:** E00. **Service:** platform. **Layer:** ci. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P03; [ ] P07; [ ] P09

**User story:** As a reviewer, I need reproducible automated evidence before accepting any milestone PR.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** .github/workflows/milestone3.yml; .gitignore; docs/milestone3/ci.md

**Technical subtasks:**
- [ ] Provision a documented Windows SQL Server LocalDB environment or explicit SQL Server test instance; verify prerequisite availability and fail clearly rather than skip tests.
- [ ] Restore/build with warnings treated as errors, verify formatting, run every test project and publish generated reports as CI artifacts.
- [ ] Run locked frontend dependency install, typecheck/lint/tests/build, architecture tests and auth suites; configure required checks and keep secrets/build/coverage output untracked.

**Specific acceptance criteria:**
- [ ] CI covers all six APIs and frontend; no silent skipped integration/auth tests, no production secret files, zero warnings/errors/failing tests required. Runtime/dependency/tool versions are pinned to supported compatible versions and lockfiles committed. Required-check names match branch protection. Test reports and redacted diagnostics are downloadable.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** A real PR pipeline passes; deliberate architecture or test failure blocks merge. Commands match the shared validation contract and successful run URL is recorded.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F00; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S01-T01 — Implement Identity domain models and pure business rules

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F01. **Epic:** E00. **Service:** Identity. **Layer:** domain. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P03

**User story:** As a signed-in player, I can see my own provider-backed profile without giving my identity-provider password to QuizArena.

**Current baseline / sources:** AuthController.cs, AuthService.cs, IUserRepository.cs, UserRepository.cs; User currently sits in Domain/Lobby/Entities/User.cs despite its Identity namespace.

**Target files / assets:** src/QuizArena.Identity.Domain/{Entities,Enums,Exceptions,Services}/; tests/QuizArena.Identity.UnitTests/Domain/

**Technical subtasks:**
- [ ] Move owned entities into the correct service Domain project and remove misplaced namespace/folder ownership.
- [ ] Implement the explicitly specified invariants and typed domain exceptions as pure in-memory operations.
- [ ] Add focused domain unit tests for successful transitions and every specified boundary; verify zero external package or outer-layer references.

**Specific acceptance criteria:**
- [ ] Model a player profile with internal Guid Id, required issuer and opaque subject, and display username; enforce nonblank subject/issuer and bounded display fields. Remove credential verification/token issuance from the profile capability; the external OIDC provider owns login. Keep legacy password hashes only in the migration archive, never public contracts.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run this service's domain unit tests and architecture tests; invalid states are rejected before persistence.

**Agent / workflow:** Domain Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F01; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S01-T02 — Implement Identity EF mappings, migrations and repositories

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F01. **Epic:** E00. **Service:** Identity. **Layer:** data. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S01-T01; [ ] P04

**User story:** As a signed-in player, I can see my own provider-backed profile without giving my identity-provider password to QuizArena.

**Current baseline / sources:** AuthController.cs, AuthService.cs, IUserRepository.cs, UserRepository.cs; User currently sits in Domain/Lobby/Entities/User.cs despite its Identity namespace.

**Target files / assets:** src/QuizArena.Identity.Data/{Context,Configurations,Repositories,Migrations}/; tests/QuizArena.Identity.IntegrationTests/Persistence/

**Technical subtasks:**
- [ ] Implement service-owned DbContext and separate entity configurations using the approved schema and preserved constraints.
- [ ] Implement repository interfaces/implementations, cancellation and AsNoTracking on read-only queries; separate persistence from domain decisions.
- [ ] Generate owned migrations and add relational persistence, uniqueness/concurrency and fresh-context verification tests against disposable SQL Server.

**Specific acceptance criteria:**
- [ ] IdentityDbContext owns UserProfiles, with unique (Issuer, Subject); profile provisioning is idempotent under concurrent calls. Migrate existing Users through the approved subject mapping; unmapped legacy accounts remain archived and inaccessible via a guessed identity.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Apply migrations to a fresh disposable service database, run relational tests and verify reads after new context/restart; no access to other service tables.

**Agent / workflow:** Data & Persistence Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F01; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S01-T03 — Implement Identity API DTOs and application orchestration

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F01. **Epic:** E00. **Service:** Identity. **Layer:** api. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S01-T01; [ ] S01-T02; [ ] P02

**User story:** As a signed-in player, I can see my own provider-backed profile without giving my identity-provider password to QuizArena.

**Current baseline / sources:** AuthController.cs, AuthService.cs, IUserRepository.cs, UserRepository.cs; User currently sits in Domain/Lobby/Entities/User.cs despite its Identity namespace.

**Target files / assets:** src/QuizArena.Identity.Api/{Contracts,Services,Clients}/; tests/QuizArena.Identity.UnitTests/Services/

**Technical subtasks:**
- [ ] Create approved request/response DTOs and application interfaces inside Api; remove HTTP DTO dependencies from Domain.
- [ ] Implement orchestration through own repositories/domain rules and, when specified, typed HTTP adapters; use constructor injection and cancellation end to end.
- [ ] Test mappings, dependency failures, trusted identity use, idempotency and specified validation with mocks; never serialize EF entities.

**Specific acceptance criteria:**
- [ ] Replace IAuthService with IProfileService in Api/Services. Resolve current issuer/subject from the validated principal through an API abstraction; provision/update profile from trusted claims, return UserProfileResponse {userId:string, username:string, displayName:string}. Remove runtime local password hashing and hand-built HMAC token code.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run service application unit tests and architecture checks; assert DTO shapes and no cross-service project references.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F01; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S01-T04 — Expose and secure Identity REST endpoints with OpenAPI

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F01. **Epic:** E00. **Service:** Identity. **Layer:** api. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S01-T03; [ ] P06

**User story:** As a signed-in player, I can see my own provider-backed profile without giving my identity-provider password to QuizArena.

**Current baseline / sources:** AuthController.cs, AuthService.cs, IUserRepository.cs, UserRepository.cs; User currently sits in Domain/Lobby/Entities/User.cs despite its Identity namespace.

**Target files / assets:** src/QuizArena.Identity.Api/{Controllers,Security,Program.cs}; contracts/openapi/identity.yaml; tests/QuizArena.Identity.IntegrationTests/Endpoints/

**Technical subtasks:**
- [ ] Implement the exact routes/status codes and thin controller mappings; map domain exceptions to safe ProblemDetails.
- [ ] Apply named scope/service/ownership policies from the matrix, expose only public DTOs and publish OpenAPI request/response/error/security definitions.
- [ ] Test success/error HTTP paths using the real bearer host; update contract snapshots and verify Location headers where relevant.

**Specific acceptance criteria:**
- [ ] GET /api/users/me => 200 profile, profiles.read; PUT /api/users/me => 200 profile, profiles.write, request {displayName:string}. Retire POST /api/auth/register and /api/auth/login with 410 ProblemDetails and documented OIDC redirect guidance; do not accept password payloads or mint tokens. GET /api/users/{id} is replaced by /me and returns 410 during documented transition.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run endpoint and security tests; compare OpenAPI to P02 contract and exercise /health without authentication.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F01; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S01-T05 — Build Identity React screens and REST integration

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F01. **Epic:** E00. **Service:** Identity. **Layer:** frontend. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P07; [ ] P02
**Live integration / completion blockers:** S01-T04. Frontend may develop against P02-approved mock HTTP before these close; do not declare live acceptance until the endpoints exist.

**User story:** As a signed-in player, I can see my own provider-backed profile without giving my identity-provider password to QuizArena.

**Current baseline / sources:** AuthController.cs, AuthService.cs, IUserRepository.cs, UserRepository.cs; User currently sits in Domain/Lobby/Entities/User.cs despite its Identity namespace.

**Target files / assets:** src/QuizArena.Client/src/features/identity/; src/QuizArena.Client/src/api/identity.ts; corresponding component tests

**Technical subtasks:**
- [ ] Implement the specified screen(s), forms/navigation and feature-local API module against committed public contracts; use mock REST until live endpoints exist.
- [ ] Add loading, empty, field validation, submit state, cancellation and user-friendly 400/401/403/404/409/503 responses as applicable.
- [ ] Test component behavior and connect to the real owned API before closure; use only session subject/public DTOs and no internal secrets or database dependencies.

**Specific acceptance criteria:**
- [ ] ProfilePage.tsx: provider sign-in/sign-out buttons, current profile, display-name edit form; show 401 sign-in action, 403 permission message, field validation and saving state. No QuizArena password form.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run frontend typecheck/lint/component tests/build; manually exercise the screen against S01-T04. Live acceptance is blocked until that issue finishes, but UI implementation may start after P02/P07.

**Agent / workflow:** Frontend Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F01; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S01-T06 — Complete Identity vertical-slice, authorization and regression coverage

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F01. **Epic:** E00. **Service:** Identity. **Layer:** testing. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S01-T04; [ ] S01-T05; [ ] P09

**User story:** As a signed-in player, I can see my own provider-backed profile without giving my identity-provider password to QuizArena.

**Current baseline / sources:** AuthController.cs, AuthService.cs, IUserRepository.cs, UserRepository.cs; User currently sits in Domain/Lobby/Entities/User.cs despite its Identity namespace.

**Target files / assets:** tests/QuizArena.Identity.{UnitTests,IntegrationTests}/; src/QuizArena.Client/src/features/identity/; docs/milestone3/validation-results.md

**Technical subtasks:**
- [ ] Audit tests already written with each layer task; add missing business, repository, endpoint and UI cases specified here.
- [ ] Run the complete positive/negative token and scope matrix plus self/participant/service-principal isolation applicable to this service.
- [ ] Verify persisted outcomes through a fresh context, deterministic test isolation and live frontend/backend acceptance; record actual commands/results without skipping failures.

**Specific acceptance criteria:**
- [ ] Unit-test trusted subject resolution, required fields, idempotent provisioning, duplicate mapping and missing claims. HTTP-test /me read/update, all bearer rejection cases, cross-user impersonation attempts, and 410 legacy endpoints. UI-test sign-in redirect invocation, profile rendering/edit success and validation/error states.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run all Identity unit/integration tests and frontend feature tests, then solution regression and architecture checks; record failures/remediation and real human Gate 2 evidence.

**Agent / workflow:** Testing Agent. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F01; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S02-T01 — Implement Statistics domain models and pure business rules

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F02. **Epic:** E00. **Service:** Statistics. **Layer:** domain. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P03

**User story:** As a player, I can view my match history, totals and leaderboard, and only trusted gameplay can submit results.

**Current baseline / sources:** StatisticsController.cs, StatisticsService.cs, IStatisticsRepository.cs, StatisticsRepository.cs; MatchResult currently sits in Domain/Lobby/Entities/MatchResult.cs despite its Statistics namespace.

**Target files / assets:** src/QuizArena.Statistics.Domain/{Entities,Enums,Exceptions,Services}/; tests/QuizArena.Statistics.UnitTests/Domain/

**Technical subtasks:**
- [ ] Move owned entities into the correct service Domain project and remove misplaced namespace/folder ownership.
- [ ] Implement the explicitly specified invariants and typed domain exceptions as pure in-memory operations.
- [ ] Add focused domain unit tests for successful transitions and every specified boundary; verify zero external package or outer-layer references.

**Specific acceptance criteria:**
- [ ] Use opaque string UserId and string MatchId consistently with Gameplay; scores are nonnegative, IDs nonblank, PlayedAt UTC. Pure aggregation calculates games, wins and total score; draws count as games but not wins; leaderboard order is TotalScore descending, Wins descending, UserId ordinal as deterministic tie-break.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run this service's domain unit tests and architecture tests; invalid states are rejected before persistence.

**Agent / workflow:** Domain Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F02; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S02-T02 — Implement Statistics EF mappings, migrations and repositories

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F02. **Epic:** E00. **Service:** Statistics. **Layer:** data. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S02-T01; [ ] P04

**User story:** As a player, I can view my match history, totals and leaderboard, and only trusted gameplay can submit results.

**Current baseline / sources:** StatisticsController.cs, StatisticsService.cs, IStatisticsRepository.cs, StatisticsRepository.cs; MatchResult currently sits in Domain/Lobby/Entities/MatchResult.cs despite its Statistics namespace.

**Target files / assets:** src/QuizArena.Statistics.Data/{Context,Configurations,Repositories,Migrations}/; tests/QuizArena.Statistics.IntegrationTests/Persistence/

**Technical subtasks:**
- [ ] Implement service-owned DbContext and separate entity configurations using the approved schema and preserved constraints.
- [ ] Implement repository interfaces/implementations, cancellation and AsNoTracking on read-only queries; separate persistence from domain decisions.
- [ ] Generate owned migrations and add relational persistence, uniqueness/concurrency and fresh-context verification tests against disposable SQL Server.

**Specific acceptance criteria:**
- [ ] StatisticsDbContext owns MatchResults; preserve unique (MatchId, UserId), UserId index and timestamp. Upsert delivery semantics: identical existing result succeeds without duplication; conflicting same-key score/outcome => domain conflict. Support paged history and bounded leaderboard queries with AsNoTracking.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Apply migrations to a fresh disposable service database, run relational tests and verify reads after new context/restart; no access to other service tables.

**Agent / workflow:** Data & Persistence Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F02; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S02-T03 — Implement Statistics API DTOs and application orchestration

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F02. **Epic:** E00. **Service:** Statistics. **Layer:** api. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S02-T01; [ ] S02-T02; [ ] P02

**User story:** As a player, I can view my match history, totals and leaderboard, and only trusted gameplay can submit results.

**Current baseline / sources:** StatisticsController.cs, StatisticsService.cs, IStatisticsRepository.cs, StatisticsRepository.cs; MatchResult currently sits in Domain/Lobby/Entities/MatchResult.cs despite its Statistics namespace.

**Target files / assets:** src/QuizArena.Statistics.Api/{Contracts,Services,Clients}/; tests/QuizArena.Statistics.UnitTests/Services/

**Technical subtasks:**
- [ ] Create approved request/response DTOs and application interfaces inside Api; remove HTTP DTO dependencies from Domain.
- [ ] Implement orchestration through own repositories/domain rules and, when specified, typed HTTP adapters; use constructor injection and cancellation end to end.
- [ ] Test mappings, dependency failures, trusted identity use, idempotency and specified validation with mocks; never serialize EF entities.

**Specific acceptance criteria:**
- [ ] Move DTOs/interfaces into Api; RecordResult application service accepts trusted internal delivery only and applies idempotency. Self history/stats derive subject from claims; top must be 1..100, default 10. Empty stats => zero totals; empty history/leaderboard => [].
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run service application unit tests and architecture checks; assert DTO shapes and no cross-service project references.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F02; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S02-T04 — Expose and secure Statistics REST endpoints with OpenAPI

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F02. **Epic:** E00. **Service:** Statistics. **Layer:** api. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S02-T03; [ ] P06

**User story:** As a player, I can view my match history, totals and leaderboard, and only trusted gameplay can submit results.

**Current baseline / sources:** StatisticsController.cs, StatisticsService.cs, IStatisticsRepository.cs, StatisticsRepository.cs; MatchResult currently sits in Domain/Lobby/Entities/MatchResult.cs despite its Statistics namespace.

**Target files / assets:** src/QuizArena.Statistics.Api/{Controllers,Security,Program.cs}; contracts/openapi/statistics.yaml; tests/QuizArena.Statistics.IntegrationTests/Endpoints/

**Technical subtasks:**
- [ ] Implement the exact routes/status codes and thin controller mappings; map domain exceptions to safe ProblemDetails.
- [ ] Apply named scope/service/ownership policies from the matrix, expose only public DTOs and publish OpenAPI request/response/error/security definitions.
- [ ] Test success/error HTTP paths using the real bearer host; update contract snapshots and verify Location headers where relevant.

**Specific acceptance criteria:**
- [ ] POST /api/results => 200 empty on first or identical retry, results.write service-only; conflicting duplicate =>409. GET /api/players/{userId}/stats =>200, stats.read and self; GET /api/players/{userId}/matches?page=1&pageSize=20 =>200, stats.read and self; invalid page/pageSize (1..100)=>400. GET /api/leaderboard?top=10 =>200, leaderboard.read, all signed-in players. Other user's self-only stats/history=>403.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run endpoint and security tests; compare OpenAPI to P02 contract and exercise /health without authentication.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F02; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S02-T05 — Build Statistics React screens and REST integration

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F02. **Epic:** E00. **Service:** Statistics. **Layer:** frontend. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P07; [ ] P02
**Live integration / completion blockers:** S02-T04. Frontend may develop against P02-approved mock HTTP before these close; do not declare live acceptance until the endpoints exist.

**User story:** As a player, I can view my match history, totals and leaderboard, and only trusted gameplay can submit results.

**Current baseline / sources:** StatisticsController.cs, StatisticsService.cs, IStatisticsRepository.cs, StatisticsRepository.cs; MatchResult currently sits in Domain/Lobby/Entities/MatchResult.cs despite its Statistics namespace.

**Target files / assets:** src/QuizArena.Client/src/features/statistics/; src/QuizArena.Client/src/api/statistics.ts; corresponding component tests

**Technical subtasks:**
- [ ] Implement the specified screen(s), forms/navigation and feature-local API module against committed public contracts; use mock REST until live endpoints exist.
- [ ] Add loading, empty, field validation, submit state, cancellation and user-friendly 400/401/403/404/409/503 responses as applicable.
- [ ] Test component behavior and connect to the real owned API before closure; use only session subject/public DTOs and no internal secrets or database dependencies.

**Specific acceptance criteria:**
- [ ] StatisticsPage.tsx and LeaderboardPage.tsx: current player's totals, paged newest-first history, leaderboard top selector; empty-state text, loading/error/retry, no result-submission form.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run frontend typecheck/lint/component tests/build; manually exercise the screen against S02-T04. Live acceptance is blocked until that issue finishes, but UI implementation may start after P02/P07.

**Agent / workflow:** Frontend Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F02; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S02-T06 — Complete Statistics vertical-slice, authorization and regression coverage

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F02. **Epic:** E00. **Service:** Statistics. **Layer:** testing. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S02-T04; [ ] S02-T05; [ ] P09

**User story:** As a player, I can view my match history, totals and leaderboard, and only trusted gameplay can submit results.

**Current baseline / sources:** StatisticsController.cs, StatisticsService.cs, IStatisticsRepository.cs, StatisticsRepository.cs; MatchResult currently sits in Domain/Lobby/Entities/MatchResult.cs despite its Statistics namespace.

**Target files / assets:** tests/QuizArena.Statistics.{UnitTests,IntegrationTests}/; src/QuizArena.Client/src/features/statistics/; docs/milestone3/validation-results.md

**Technical subtasks:**
- [ ] Audit tests already written with each layer task; add missing business, repository, endpoint and UI cases specified here.
- [ ] Run the complete positive/negative token and scope matrix plus self/participant/service-principal isolation applicable to this service.
- [ ] Verify persisted outcomes through a fresh context, deterministic test isolation and live frontend/backend acceptance; record actual commands/results without skipping failures.

**Specific acceptance criteria:**
- [ ] Unit-test aggregation, draws, stable ties, top/page bounds and duplicate semantics. HTTP-test self/other-user rules, service-only ingestion, exact retry/conflict, empty collections and persistence from a fresh context. UI-test totals/history, pagination, empty/error states and leaderboard rendering.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run all Statistics unit/integration tests and frontend feature tests, then solution regression and architecture checks; record failures/remediation and real human Gate 2 evidence.

**Agent / workflow:** Testing Agent. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F02; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S03-T01 — Implement QuestionBank domain models and pure business rules

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F03. **Epic:** E00. **Service:** QuestionBank. **Layer:** domain. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P03

**User story:** As a player, I can browse available quiz content, while Gameplay securely obtains the answer material needed for a real persisted match.

**Current baseline / sources:** QuestionsController.cs, QuestionService.cs, IQuestionRepository.cs, QuestionRepository.cs, Question and Category entities.

**Target files / assets:** src/QuizArena.QuestionBank.Domain/{Entities,Enums,Exceptions,Services}/; tests/QuizArena.QuestionBank.UnitTests/Domain/

**Technical subtasks:**
- [ ] Move owned entities into the correct service Domain project and remove misplaced namespace/folder ownership.
- [ ] Implement the explicitly specified invariants and typed domain exceptions as pure in-memory operations.
- [ ] Add focused domain unit tests for successful transitions and every specified boundary; verify zero external package or outer-layer references.

**Specific acceptance criteria:**
- [ ] Question has Guid Id, nonblank prompt/category, exactly four distinct nonblank options, and one correct answer matching exactly one option. Pure selection rules require count 1..50. Categories and catalog content belong only to QuestionBank. Internal immutable match snapshots prevent catalog changes from changing an in-progress match.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run this service's domain unit tests and architecture tests; invalid states are rejected before persistence.

**Agent / workflow:** Domain Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F03; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S03-T02 — Implement QuestionBank EF mappings, migrations and repositories

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F03. **Epic:** E00. **Service:** QuestionBank. **Layer:** data. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S03-T01; [ ] P04

**User story:** As a player, I can browse available quiz content, while Gameplay securely obtains the answer material needed for a real persisted match.

**Current baseline / sources:** QuestionsController.cs, QuestionService.cs, IQuestionRepository.cs, QuestionRepository.cs, Question and Category entities.

**Target files / assets:** src/QuizArena.QuestionBank.Data/{Context,Configurations,Repositories,Migrations}/; tests/QuizArena.QuestionBank.IntegrationTests/Persistence/

**Technical subtasks:**
- [ ] Implement service-owned DbContext and separate entity configurations using the approved schema and preserved constraints.
- [ ] Implement repository interfaces/implementations, cancellation and AsNoTracking on read-only queries; separate persistence from domain decisions.
- [ ] Generate owned migrations and add relational persistence, uniqueness/concurrency and fresh-context verification tests against disposable SQL Server.

**Specific acceptance criteria:**
- [ ] QuestionBankDbContext owns Questions, Categories and question options; separate IEntityTypeConfiguration classes preserve restricted category deletion and required fields. Add a deterministic catalog with at least five valid questions. Random selection uses no replacement; count above available content =>409 for internal exact-count sessions, public generate returns up to count.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Apply migrations to a fresh disposable service database, run relational tests and verify reads after new context/restart; no access to other service tables.

**Agent / workflow:** Data & Persistence Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F03; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S03-T03 — Implement QuestionBank API DTOs and application orchestration

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F03. **Epic:** E00. **Service:** QuestionBank. **Layer:** api. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S03-T01; [ ] S03-T02; [ ] P02

**User story:** As a player, I can browse available quiz content, while Gameplay securely obtains the answer material needed for a real persisted match.

**Current baseline / sources:** QuestionsController.cs, QuestionService.cs, IQuestionRepository.cs, QuestionRepository.cs, Question and Category entities.

**Target files / assets:** src/QuizArena.QuestionBank.Api/{Contracts,Services,Clients}/; tests/QuizArena.QuestionBank.UnitTests/Services/

**Technical subtasks:**
- [ ] Create approved request/response DTOs and application interfaces inside Api; remove HTTP DTO dependencies from Domain.
- [ ] Implement orchestration through own repositories/domain rules and, when specified, typed HTTP adapters; use constructor injection and cancellation end to end.
- [ ] Test mappings, dependency failures, trusted identity use, idempotency and specified validation with mocks; never serialize EF entities.

**Specific acceptance criteria:**
- [ ] Public QuestionResponse {id:Guid,prompt,category,options:string[]} excludes correctAnswer. Internal QuestionSetResponse includes answer material only for service callers. Create IQuestionCatalogService in Api, invoke Domain validation and repository abstraction; forward cancellation throughout.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run service application unit tests and architecture checks; assert DTO shapes and no cross-service project references.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F03; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S03-T04 — Expose and secure QuestionBank REST endpoints with OpenAPI

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F03. **Epic:** E00. **Service:** QuestionBank. **Layer:** api. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S03-T03; [ ] P06

**User story:** As a player, I can browse available quiz content, while Gameplay securely obtains the answer material needed for a real persisted match.

**Current baseline / sources:** QuestionsController.cs, QuestionService.cs, IQuestionRepository.cs, QuestionRepository.cs, Question and Category entities.

**Target files / assets:** src/QuizArena.QuestionBank.Api/{Controllers,Security,Program.cs}; contracts/openapi/questionbank.yaml; tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/

**Technical subtasks:**
- [ ] Implement the exact routes/status codes and thin controller mappings; map domain exceptions to safe ProblemDetails.
- [ ] Apply named scope/service/ownership policies from the matrix, expose only public DTOs and publish OpenAPI request/response/error/security definitions.
- [ ] Test success/error HTTP paths using the real bearer host; update contract snapshots and verify Location headers where relevant.

**Specific acceptance criteria:**
- [ ] GET /api/questions/generate?count=10 =>200, questions.read; count outside 1..50=>400; empty catalog=>200 []. POST /internal/question-sets {count:5} =>200 exact immutable snapshot, questions.play service-only; insufficient valid catalog=>409. No public DTO or Swagger browser sample exposes correct answers.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run endpoint and security tests; compare OpenAPI to P02 contract and exercise /health without authentication.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F03; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S03-T05 — Build QuestionBank React screens and REST integration

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F03. **Epic:** E00. **Service:** QuestionBank. **Layer:** frontend. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P07; [ ] P02
**Live integration / completion blockers:** S03-T04. Frontend may develop against P02-approved mock HTTP before these close; do not declare live acceptance until the endpoints exist.

**User story:** As a player, I can browse available quiz content, while Gameplay securely obtains the answer material needed for a real persisted match.

**Current baseline / sources:** QuestionsController.cs, QuestionService.cs, IQuestionRepository.cs, QuestionRepository.cs, Question and Category entities.

**Target files / assets:** src/QuizArena.Client/src/features/questionbank/; src/QuizArena.Client/src/api/questionbank.ts; corresponding component tests

**Technical subtasks:**
- [ ] Implement the specified screen(s), forms/navigation and feature-local API module against committed public contracts; use mock REST until live endpoints exist.
- [ ] Add loading, empty, field validation, submit state, cancellation and user-friendly 400/401/403/404/409/503 responses as applicable.
- [ ] Test component behavior and connect to the real owned API before closure; use only session subject/public DTOs and no internal secrets or database dependencies.

**Specific acceptance criteria:**
- [ ] QuestionBankPage.tsx: question-count selector, generated question preview with options/category, regenerate button, empty/loading/error/validation states; never render correct answers or call /internal/question-sets.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run frontend typecheck/lint/component tests/build; manually exercise the screen against S03-T04. Live acceptance is blocked until that issue finishes, but UI implementation may start after P02/P07.

**Agent / workflow:** Frontend Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F03; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S03-T06 — Complete QuestionBank vertical-slice, authorization and regression coverage

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F03. **Epic:** E00. **Service:** QuestionBank. **Layer:** testing. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S03-T04; [ ] S03-T05; [ ] P09

**User story:** As a player, I can browse available quiz content, while Gameplay securely obtains the answer material needed for a real persisted match.

**Current baseline / sources:** QuestionsController.cs, QuestionService.cs, IQuestionRepository.cs, QuestionRepository.cs, Question and Category entities.

**Target files / assets:** tests/QuizArena.QuestionBank.{UnitTests,IntegrationTests}/; src/QuizArena.Client/src/features/questionbank/; docs/milestone3/validation-results.md

**Technical subtasks:**
- [ ] Audit tests already written with each layer task; add missing business, repository, endpoint and UI cases specified here.
- [ ] Run the complete positive/negative token and scope matrix plus self/participant/service-principal isolation applicable to this service.
- [ ] Verify persisted outcomes through a fresh context, deterministic test isolation and live frontend/backend acceptance; record actual commands/results without skipping failures.

**Specific acceptance criteria:**
- [ ] Unit-test option/answer invariants, count bounds, no duplicate selection and answer-free DTO mapping. HTTP-test both endpoints, exact-count shortage, service-only answer access and absence of correctAnswer in public JSON. UI-test generation, input bounds, errors and answer secrecy.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run all QuestionBank unit/integration tests and frontend feature tests, then solution regression and architecture checks; record failures/remediation and real human Gate 2 evidence.

**Agent / workflow:** Testing Agent. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F03; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S04-T01 — Implement Lobby domain models and pure business rules

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F04. **Epic:** E00. **Service:** Lobby. **Layer:** domain. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P03

**User story:** As a player, I can create or join a two-player lobby and, as its host, lock it before starting a match.

**Current baseline / sources:** LobbiesController.cs, LobbyService.cs, ILobbyRepository.cs, LobbyRepository.cs, ActiveLobby and LobbyPlayer; LobbyCreateRequest exists but no create endpoint exists.

**Target files / assets:** src/QuizArena.Lobby.Domain/{Entities,Enums,Exceptions,Services}/; tests/QuizArena.Lobby.UnitTests/Domain/

**Technical subtasks:**
- [ ] Move owned entities into the correct service Domain project and remove misplaced namespace/folder ownership.
- [ ] Implement the explicitly specified invariants and typed domain exceptions as pure in-memory operations.
- [ ] Add focused domain unit tests for successful transitions and every specified boundary; verify zero external package or outer-layer references.

**Specific acceptance criteria:**
- [ ] Lobby capacity is exactly two distinct subjects including host; states Open -> Locked. Join allowed only while Open; repeat join by same subject is idempotent; third player or join to Locked =>409. Only host locks, and only with exactly two players; lock repeat returns existing locked state. Generate normalized uppercase six-character alphanumeric join codes.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run this service's domain unit tests and architecture tests; invalid states are rejected before persistence.

**Agent / workflow:** Domain Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F04; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S04-T02 — Implement Lobby EF mappings, migrations and repositories

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F04. **Epic:** E00. **Service:** Lobby. **Layer:** data. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S04-T01; [ ] P04

**User story:** As a player, I can create or join a two-player lobby and, as its host, lock it before starting a match.

**Current baseline / sources:** LobbiesController.cs, LobbyService.cs, ILobbyRepository.cs, LobbyRepository.cs, ActiveLobby and LobbyPlayer; LobbyCreateRequest exists but no create endpoint exists.

**Target files / assets:** src/QuizArena.Lobby.Data/{Context,Configurations,Repositories,Migrations}/; tests/QuizArena.Lobby.IntegrationTests/Persistence/

**Technical subtasks:**
- [ ] Implement service-owned DbContext and separate entity configurations using the approved schema and preserved constraints.
- [ ] Implement repository interfaces/implementations, cancellation and AsNoTracking on read-only queries; separate persistence from domain decisions.
- [ ] Generate owned migrations and add relational persistence, uniqueness/concurrency and fresh-context verification tests against disposable SQL Server.

**Specific acceptance criteria:**
- [ ] LobbyDbContext owns ActiveLobbies/LobbyPlayers; preserve unique JoinCode, add unique (LobbyId,PlayerId), cascade player deletion, row-version concurrency. Add create/get/join/save operations, handle code collisions with bounded retry, concurrent last-slot join cannot exceed capacity.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Apply migrations to a fresh disposable service database, run relational tests and verify reads after new context/restart; no access to other service tables.

**Agent / workflow:** Data & Persistence Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F04; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S04-T03 — Implement Lobby API DTOs and application orchestration

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F04. **Epic:** E00. **Service:** Lobby. **Layer:** api. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S04-T01; [ ] S04-T02; [ ] P02

**User story:** As a player, I can create or join a two-player lobby and, as its host, lock it before starting a match.

**Current baseline / sources:** LobbiesController.cs, LobbyService.cs, ILobbyRepository.cs, LobbyRepository.cs, ActiveLobby and LobbyPlayer; LobbyCreateRequest exists but no create endpoint exists.

**Target files / assets:** src/QuizArena.Lobby.Api/{Contracts,Services,Clients}/; tests/QuizArena.Lobby.UnitTests/Services/

**Technical subtasks:**
- [ ] Create approved request/response DTOs and application interfaces inside Api; remove HTTP DTO dependencies from Domain.
- [ ] Implement orchestration through own repositories/domain rules and, when specified, typed HTTP adapters; use constructor injection and cancellation end to end.
- [ ] Test mappings, dependency failures, trusted identity use, idempotency and specified validation with mocks; never serialize EF entities.

**Specific acceptance criteria:**
- [ ] Api contracts derive host/joining subject from trusted principal; clients never choose HostId or PlayerId for themselves. Create/get/join/lock workflows call Domain invariants. Internal locked-lobby lookup returns host, two subjects and state to Gameplay. Lobby does not create matches or touch Gameplay tables.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run service application unit tests and architecture checks; assert DTO shapes and no cross-service project references.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F04; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S04-T04 — Expose and secure Lobby REST endpoints with OpenAPI

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F04. **Epic:** E00. **Service:** Lobby. **Layer:** api. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S04-T03; [ ] P06

**User story:** As a player, I can create or join a two-player lobby and, as its host, lock it before starting a match.

**Current baseline / sources:** LobbiesController.cs, LobbyService.cs, ILobbyRepository.cs, LobbyRepository.cs, ActiveLobby and LobbyPlayer; LobbyCreateRequest exists but no create endpoint exists.

**Target files / assets:** src/QuizArena.Lobby.Api/{Controllers,Security,Program.cs}; contracts/openapi/lobby.yaml; tests/QuizArena.Lobby.IntegrationTests/Endpoints/

**Technical subtasks:**
- [ ] Implement the exact routes/status codes and thin controller mappings; map domain exceptions to safe ProblemDetails.
- [ ] Apply named scope/service/ownership policies from the matrix, expose only public DTOs and publish OpenAPI request/response/error/security definitions.
- [ ] Test success/error HTTP paths using the real bearer host; update contract snapshots and verify Location headers where relevant.

**Specific acceptance criteria:**
- [ ] POST /api/lobbies {} =>201, lobbies.write, Location points to GET; GET /api/lobbies/{joinCode} =>200, lobbies.read, sanitized join preview; POST /api/lobbies/{joinCode}/join {} =>200, lobbies.write; POST /api/lobbies/{joinCode}/lock =>200, lobbies.write plus host rule. Missing=>404; invalid joinCode=>400; invalid state/capacity=>409; nonhost lock=>403. GET /internal/lobbies/{joinCode} =>200, lobbies.verify service-only.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run endpoint and security tests; compare OpenAPI to P02 contract and exercise /health without authentication.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F04; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S04-T05 — Build Lobby React screens and REST integration

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F04. **Epic:** E00. **Service:** Lobby. **Layer:** frontend. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P07; [ ] P02
**Live integration / completion blockers:** S04-T04, S05-T04. Frontend may develop against P02-approved mock HTTP before these close; do not declare live acceptance until the endpoints exist.

**User story:** As a player, I can create or join a two-player lobby and, as its host, lock it before starting a match.

**Current baseline / sources:** LobbiesController.cs, LobbyService.cs, ILobbyRepository.cs, LobbyRepository.cs, ActiveLobby and LobbyPlayer; LobbyCreateRequest exists but no create endpoint exists.

**Target files / assets:** src/QuizArena.Client/src/features/lobby/; src/QuizArena.Client/src/api/lobby.ts; corresponding component tests

**Technical subtasks:**
- [ ] Implement the specified screen(s), forms/navigation and feature-local API module against committed public contracts; use mock REST until live endpoints exist.
- [ ] Add loading, empty, field validation, submit state, cancellation and user-friendly 400/401/403/404/409/503 responses as applicable.
- [ ] Test component behavior and connect to the real owned API before closure; use only session subject/public DTOs and no internal secrets or database dependencies.

**Specific acceptance criteria:**
- [ ] LobbyPage.tsx: create/join forms, join code display/copy, host/player membership and lock state; host-only lock/start controls, polling and cleanup; Start invokes Gameplay create after lock, displays navigation to returned match.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run frontend typecheck/lint/component tests/build; manually exercise the screen against S04-T04. Live acceptance is blocked until that issue finishes, but UI implementation may start after P02/P07.

**Agent / workflow:** Frontend Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F04; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S04-T06 — Complete Lobby vertical-slice, authorization and regression coverage

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F04. **Epic:** E00. **Service:** Lobby. **Layer:** testing. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S04-T04; [ ] S04-T05; [ ] P09

**User story:** As a player, I can create or join a two-player lobby and, as its host, lock it before starting a match.

**Current baseline / sources:** LobbiesController.cs, LobbyService.cs, ILobbyRepository.cs, LobbyRepository.cs, ActiveLobby and LobbyPlayer; LobbyCreateRequest exists but no create endpoint exists.

**Target files / assets:** tests/QuizArena.Lobby.{UnitTests,IntegrationTests}/; src/QuizArena.Client/src/features/lobby/; docs/milestone3/validation-results.md

**Technical subtasks:**
- [ ] Audit tests already written with each layer task; add missing business, repository, endpoint and UI cases specified here.
- [ ] Run the complete positive/negative token and scope matrix plus self/participant/service-principal isolation applicable to this service.
- [ ] Verify persisted outcomes through a fresh context, deterministic test isolation and live frontend/backend acceptance; record actual commands/results without skipping failures.

**Specific acceptance criteria:**
- [ ] Unit-test create/join/duplicate/full/locked/host transitions. Relational-test code uniqueness, membership uniqueness and concurrent capacity. HTTP-test create/get/join/lock/internal lookup, spoofed host, 403/404/409, persisted lock. UI-test two-player create/join/lock/start, errors and disabled nonhost controls.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run all Lobby unit/integration tests and frontend feature tests, then solution regression and architecture checks; record failures/remediation and real human Gate 2 evidence.

**Agent / workflow:** Testing Agent. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F04; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S05-T01 — Implement Gameplay domain models and pure business rules

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F05. **Epic:** E00. **Service:** Gameplay. **Layer:** domain. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P03

**User story:** As one of two lobby players, I can complete a fair five-round persisted quiz match and see a result that reaches Statistics and Notifications.

**Current baseline / sources:** MatchesController.cs, GameplayService.cs, IMatchRepository.cs, MatchRepository.cs, ActiveMatch/PlayerState. Current mock questions, direct INotificationService call, unchecked submissions and no automatic Statistics ingestion need replacement.

**Target files / assets:** src/QuizArena.Gameplay.Domain/{Entities,Enums,Exceptions,Services}/; tests/QuizArena.Gameplay.UnitTests/Domain/

**Technical subtasks:**
- [ ] Move owned entities into the correct service Domain project and remove misplaced namespace/folder ownership.
- [ ] Implement the explicitly specified invariants and typed domain exceptions as pure in-memory operations.
- [ ] Add focused domain unit tests for successful transitions and every specified boundary; verify zero external package or outer-layer references.

**Specific acceptance criteria:**
- [ ] Five rounds, each player answers each round once; advance only after both answers; correct answer earns one point. Match membership required, current snapshot question must match, finished match immutable. Duplicate/wrong-current question =>409, outsider=>403. Persist answers, question snapshots and scores; winner is higher score, tie=>draw with no winner. GET never advances state.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run this service's domain unit tests and architecture tests; invalid states are rejected before persistence.

**Agent / workflow:** Domain Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F05; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S05-T02 — Implement Gameplay EF mappings, migrations and repositories

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F05. **Epic:** E00. **Service:** Gameplay. **Layer:** data. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S05-T01; [ ] P04

**User story:** As one of two lobby players, I can complete a fair five-round persisted quiz match and see a result that reaches Statistics and Notifications.

**Current baseline / sources:** MatchesController.cs, GameplayService.cs, IMatchRepository.cs, MatchRepository.cs, ActiveMatch/PlayerState. Current mock questions, direct INotificationService call, unchecked submissions and no automatic Statistics ingestion need replacement.

**Target files / assets:** src/QuizArena.Gameplay.Data/{Context,Configurations,Repositories,Migrations}/; tests/QuizArena.Gameplay.IntegrationTests/Persistence/

**Technical subtasks:**
- [ ] Implement service-owned DbContext and separate entity configurations using the approved schema and preserved constraints.
- [ ] Implement repository interfaces/implementations, cancellation and AsNoTracking on read-only queries; separate persistence from domain decisions.
- [ ] Generate owned migrations and add relational persistence, uniqueness/concurrency and fresh-context verification tests against disposable SQL Server.

**Specific acceptance criteria:**
- [ ] GameplayDbContext owns matches, player states, snapshots, answers, and PendingDeliveries. Unique (MatchId,QuestionIndex,UserId), row-version or optimistic concurrency prevents double scoring. Match completion plus pending result/event deliveries commit atomically in this database. Persist deterministic delivery IDs so retries/restarts cannot duplicate downstream effects.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Apply migrations to a fresh disposable service database, run relational tests and verify reads after new context/restart; no access to other service tables.

**Agent / workflow:** Data & Persistence Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F05; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S05-T03 — Implement Gameplay API DTOs and application orchestration

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F05. **Epic:** E00. **Service:** Gameplay. **Layer:** api. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S05-T01; [ ] S05-T02; [ ] P02; [ ] S03-T04; [ ] S04-T04

**User story:** As one of two lobby players, I can complete a fair five-round persisted quiz match and see a result that reaches Statistics and Notifications.

**Current baseline / sources:** MatchesController.cs, GameplayService.cs, IMatchRepository.cs, MatchRepository.cs, ActiveMatch/PlayerState. Current mock questions, direct INotificationService call, unchecked submissions and no automatic Statistics ingestion need replacement.

**Target files / assets:** src/QuizArena.Gameplay.Api/{Contracts,Services,Clients}/; tests/QuizArena.Gameplay.UnitTests/Services/

**Technical subtasks:**
- [ ] Create approved request/response DTOs and application interfaces inside Api; remove HTTP DTO dependencies from Domain.
- [ ] Implement orchestration through own repositories/domain rules and, when specified, typed HTTP adapters; use constructor injection and cancellation end to end.
- [ ] Test mappings, dependency failures, trusted identity use, idempotency and specified validation with mocks; never serialize EF entities.

**Specific acceptance criteria:**
- [ ] Api orchestration creates match from {joinCode}; verifies locked lobby/host via Lobby HTTP and obtains exactly five internal QuestionBank snapshots using service token. Generate matchId server-side and allow one match per lobby; duplicate start returns existing match. Use typed HttpClient adapters; no references to other Api/Domain/Data assemblies. Derive answering subject from bearer token. Dispatch two result records and lifecycle events through HTTP; bounded retry/backoff, persisted pending state and redrive worker survive restart.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run service application unit tests and architecture checks; assert DTO shapes and no cross-service project references.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F05; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S05-T04 — Expose and secure Gameplay REST endpoints with OpenAPI

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F05. **Epic:** E00. **Service:** Gameplay. **Layer:** api. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S05-T03; [ ] P06

**User story:** As one of two lobby players, I can complete a fair five-round persisted quiz match and see a result that reaches Statistics and Notifications.

**Current baseline / sources:** MatchesController.cs, GameplayService.cs, IMatchRepository.cs, MatchRepository.cs, ActiveMatch/PlayerState. Current mock questions, direct INotificationService call, unchecked submissions and no automatic Statistics ingestion need replacement.

**Target files / assets:** src/QuizArena.Gameplay.Api/{Controllers,Security,Program.cs}; contracts/openapi/gameplay.yaml; tests/QuizArena.Gameplay.IntegrationTests/Endpoints/

**Technical subtasks:**
- [ ] Implement the exact routes/status codes and thin controller mappings; map domain exceptions to safe ProblemDetails.
- [ ] Apply named scope/service/ownership policies from the matrix, expose only public DTOs and publish OpenAPI request/response/error/security definitions.
- [ ] Test success/error HTTP paths using the real bearer host; update contract snapshots and verify Location headers where relevant.

**Specific acceptance criteria:**
- [ ] POST /matches {joinCode} =>201, matches.write and lobby host; repeat same lobby=>200 same match. GET /matches/{matchId} =>200, matches.read and participant; GET /matches/{matchId}/next-question =>200 public question, matches.read and participant; finished=>409. POST /matches/{matchId}/answers {questionId:Guid,answer:string} =>200 {correct,playerAScore,playerBScore,roundComplete,finished}, matches.write and participant. Missing=>404; invalid input=>400; replay/stale/finished=>409; dependency unavailable before creation=>503 without partial match.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run endpoint and security tests; compare OpenAPI to P02 contract and exercise /health without authentication.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F05; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S05-T05 — Build Gameplay React screens and REST integration

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F05. **Epic:** E00. **Service:** Gameplay. **Layer:** frontend. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P07; [ ] P02
**Live integration / completion blockers:** S05-T04. Frontend may develop against P02-approved mock HTTP before these close; do not declare live acceptance until the endpoints exist.

**User story:** As one of two lobby players, I can complete a fair five-round persisted quiz match and see a result that reaches Statistics and Notifications.

**Current baseline / sources:** MatchesController.cs, GameplayService.cs, IMatchRepository.cs, MatchRepository.cs, ActiveMatch/PlayerState. Current mock questions, direct INotificationService call, unchecked submissions and no automatic Statistics ingestion need replacement.

**Target files / assets:** src/QuizArena.Client/src/features/gameplay/; src/QuizArena.Client/src/api/gameplay.ts; corresponding component tests

**Technical subtasks:**
- [ ] Implement the specified screen(s), forms/navigation and feature-local API module against committed public contracts; use mock REST until live endpoints exist.
- [ ] Add loading, empty, field validation, submit state, cancellation and user-friendly 400/401/403/404/409/503 responses as applicable.
- [ ] Test component behavior and connect to the real owned API before closure; use only session subject/public DTOs and no internal secrets or database dependencies.

**Specific acceptance criteria:**
- [ ] MatchPage.tsx: current public question/options, answer submit once, waiting-for-opponent display with polling, live scores, final winner/draw and links to stats/events. Hide answer key; disable duplicate submission and handle 409 refresh, 401/403 and network retry. Live Start from Lobby must wait for S05-T04.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run frontend typecheck/lint/component tests/build; manually exercise the screen against S05-T04. Live acceptance is blocked until that issue finishes, but UI implementation may start after P02/P07.

**Agent / workflow:** Frontend Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F05; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S05-T06 — Complete Gameplay vertical-slice, authorization and regression coverage

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F05. **Epic:** E00. **Service:** Gameplay. **Layer:** testing. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S05-T04; [ ] S05-T05; [ ] P09

**User story:** As one of two lobby players, I can complete a fair five-round persisted quiz match and see a result that reaches Statistics and Notifications.

**Current baseline / sources:** MatchesController.cs, GameplayService.cs, IMatchRepository.cs, MatchRepository.cs, ActiveMatch/PlayerState. Current mock questions, direct INotificationService call, unchecked submissions and no automatic Statistics ingestion need replacement.

**Target files / assets:** tests/QuizArena.Gameplay.{UnitTests,IntegrationTests}/; src/QuizArena.Client/src/features/gameplay/; docs/milestone3/validation-results.md

**Technical subtasks:**
- [ ] Audit tests already written with each layer task; add missing business, repository, endpoint and UI cases specified here.
- [ ] Run the complete positive/negative token and scope matrix plus self/participant/service-principal isolation applicable to this service.
- [ ] Verify persisted outcomes through a fresh context, deterministic test isolation and live frontend/backend acceptance; record actual commands/results without skipping failures.

**Specific acceptance criteria:**
- [ ] Pure tests for both players, wrong/current/replayed answers, outsiders, draw, round advance and immutability. Relational tests for concurrency and atomic completion/pending delivery. HTTP tests mock external servers to verify credentials, failures, token policies and participant checks. UI-test both-player flow and completed/draw/error states; restart/retry delivery verified in cross-service E2E.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run all Gameplay unit/integration tests and frontend feature tests, then solution regression and architecture checks; record failures/remediation and real human Gate 2 evidence.

**Agent / workflow:** Testing Agent. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F05; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S06-T01 — Implement Notification domain models and pure business rules

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F06. **Epic:** E00. **Service:** Notification. **Layer:** domain. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P03

**User story:** As a player, I can read my match events and manage my notification preferences without exposing another player's data.

**Current baseline / sources:** EventsController.cs, PrefsController.cs, NotificationService.cs, IEventRepository.cs, EventRepository.cs, EventLog/UserPref entities. Currently no preference GET route and service interfaces contain UserPrefDto.

**Target files / assets:** src/QuizArena.Notification.Domain/{Entities,Enums,Exceptions,Services}/; tests/QuizArena.Notification.UnitTests/Domain/

**Technical subtasks:**
- [ ] Move owned entities into the correct service Domain project and remove misplaced namespace/folder ownership.
- [ ] Implement the explicitly specified invariants and typed domain exceptions as pure in-memory operations.
- [ ] Add focused domain unit tests for successful transitions and every specified boundary; verify zero external package or outer-layer references.

**Specific acceptance criteria:**
- [ ] Allow MATCH_CREATED and MATCH_COMPLETED events with valid typed match payload; immutable eventId for delivery deduplication. Store opaque user subject and string matchId; required IDs nonblank. Preferences default emailEnabled=false and pushEnabled=false when absent. Preferences describe future delivery choices; this milestone records events without sending email/push.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run this service's domain unit tests and architecture tests; invalid states are rejected before persistence.

**Agent / workflow:** Domain Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F06; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S06-T02 — Implement Notification EF mappings, migrations and repositories

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F06. **Epic:** E00. **Service:** Notification. **Layer:** data. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S06-T01; [ ] P04

**User story:** As a player, I can read my match events and manage my notification preferences without exposing another player's data.

**Current baseline / sources:** EventsController.cs, PrefsController.cs, NotificationService.cs, IEventRepository.cs, EventRepository.cs, EventLog/UserPref entities. Currently no preference GET route and service interfaces contain UserPrefDto.

**Target files / assets:** src/QuizArena.Notification.Data/{Context,Configurations,Repositories,Migrations}/; tests/QuizArena.Notification.IntegrationTests/Persistence/

**Technical subtasks:**
- [ ] Implement service-owned DbContext and separate entity configurations using the approved schema and preserved constraints.
- [ ] Implement repository interfaces/implementations, cancellation and AsNoTracking on read-only queries; separate persistence from domain decisions.
- [ ] Generate owned migrations and add relational persistence, uniqueness/concurrency and fresh-context verification tests against disposable SQL Server.

**Specific acceptance criteria:**
- [ ] NotificationDbContext owns EventLogs/UserPrefs; unique EventId, unique preference UserId and user/time event index. Identical eventId/payload retry returns same event; conflicting retry=>409. Preference upsert returns actual persisted record; no duplicate rows under concurrency. Read history newest-first with stable Id tie-break and pagination.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Apply migrations to a fresh disposable service database, run relational tests and verify reads after new context/restart; no access to other service tables.

**Agent / workflow:** Data & Persistence Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F06; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S06-T03 — Implement Notification API DTOs and application orchestration

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F06. **Epic:** E00. **Service:** Notification. **Layer:** api. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S06-T01; [ ] S06-T02; [ ] P02

**User story:** As a player, I can read my match events and manage my notification preferences without exposing another player's data.

**Current baseline / sources:** EventsController.cs, PrefsController.cs, NotificationService.cs, IEventRepository.cs, EventRepository.cs, EventLog/UserPref entities. Currently no preference GET route and service interfaces contain UserPrefDto.

**Target files / assets:** src/QuizArena.Notification.Api/{Contracts,Services,Clients}/; tests/QuizArena.Notification.UnitTests/Services/

**Technical subtasks:**
- [ ] Create approved request/response DTOs and application interfaces inside Api; remove HTTP DTO dependencies from Domain.
- [ ] Implement orchestration through own repositories/domain rules and, when specified, typed HTTP adapters; use constructor injection and cancellation end to end.
- [ ] Test mappings, dependency failures, trusted identity use, idempotency and specified validation with mocks; never serialize EF entities.

**Specific acceptance criteria:**
- [ ] Api contracts/interfaces own request/response DTOs. Event ingestion restricted to service principal; self event/preferences operations use validated subject. Get missing preferences returns defaults without implicit write; PUT idempotently persists preferences. Keep JSON payload serialization typed and safe; do not interpolate unescaped subject/match strings.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run service application unit tests and architecture checks; assert DTO shapes and no cross-service project references.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F06; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S06-T04 — Expose and secure Notification REST endpoints with OpenAPI

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F06. **Epic:** E00. **Service:** Notification. **Layer:** api. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S06-T03; [ ] P06

**User story:** As a player, I can read my match events and manage my notification preferences without exposing another player's data.

**Current baseline / sources:** EventsController.cs, PrefsController.cs, NotificationService.cs, IEventRepository.cs, EventRepository.cs, EventLog/UserPref entities. Currently no preference GET route and service interfaces contain UserPrefDto.

**Target files / assets:** src/QuizArena.Notification.Api/{Controllers,Security,Program.cs}; contracts/openapi/notification.yaml; tests/QuizArena.Notification.IntegrationTests/Endpoints/

**Technical subtasks:**
- [ ] Implement the exact routes/status codes and thin controller mappings; map domain exceptions to safe ProblemDetails.
- [ ] Apply named scope/service/ownership policies from the matrix, expose only public DTOs and publish OpenAPI request/response/error/security definitions.
- [ ] Test success/error HTTP paths using the real bearer host; update contract snapshots and verify Location headers where relevant.

**Specific acceptance criteria:**
- [ ] POST /events {eventId:Guid,type,userId,payload:{matchId,winnerUserId?,draw?}} =>202 persisted EventResponse, events.write service-only; exact retry same response, conflict409. GET /events?page=1&pageSize=20 =>200 self events, events.read; legacy userId query if supplied must equal caller or403. GET /users/{userId}/preferences =>200 defaults/stored, preferences.read and self; PUT same route {emailEnabled,pushEnabled} =>200, preferences.write and self. Invalid payload/page=>400; cross-user=>403.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run endpoint and security tests; compare OpenAPI to P02 contract and exercise /health without authentication.

**Agent / workflow:** API & Service Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F06; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S06-T05 — Build Notification React screens and REST integration

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F06. **Epic:** E00. **Service:** Notification. **Layer:** frontend. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P07; [ ] P02
**Live integration / completion blockers:** S06-T04. Frontend may develop against P02-approved mock HTTP before these close; do not declare live acceptance until the endpoints exist.

**User story:** As a player, I can read my match events and manage my notification preferences without exposing another player's data.

**Current baseline / sources:** EventsController.cs, PrefsController.cs, NotificationService.cs, IEventRepository.cs, EventRepository.cs, EventLog/UserPref entities. Currently no preference GET route and service interfaces contain UserPrefDto.

**Target files / assets:** src/QuizArena.Client/src/features/notification/; src/QuizArena.Client/src/api/notification.ts; corresponding component tests

**Technical subtasks:**
- [ ] Implement the specified screen(s), forms/navigation and feature-local API module against committed public contracts; use mock REST until live endpoints exist.
- [ ] Add loading, empty, field validation, submit state, cancellation and user-friendly 400/401/403/404/409/503 responses as applicable.
- [ ] Test component behavior and connect to the real owned API before closure; use only session subject/public DTOs and no internal secrets or database dependencies.

**Specific acceptance criteria:**
- [ ] NotificationsPage.tsx: self event timeline with pagination/empty state and PreferencesPage.tsx with email/push toggles, save feedback and failures; explain that external delivery is not implemented, show only actual event records.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run frontend typecheck/lint/component tests/build; manually exercise the screen against S06-T04. Live acceptance is blocked until that issue finishes, but UI implementation may start after P02/P07.

**Agent / workflow:** Frontend Developer; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F06; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### S06-T06 — Complete Notification vertical-slice, authorization and regression coverage

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F06. **Epic:** E00. **Service:** Notification. **Layer:** testing. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] S06-T04; [ ] S06-T05; [ ] P09

**User story:** As a player, I can read my match events and manage my notification preferences without exposing another player's data.

**Current baseline / sources:** EventsController.cs, PrefsController.cs, NotificationService.cs, IEventRepository.cs, EventRepository.cs, EventLog/UserPref entities. Currently no preference GET route and service interfaces contain UserPrefDto.

**Target files / assets:** tests/QuizArena.Notification.{UnitTests,IntegrationTests}/; src/QuizArena.Client/src/features/notification/; docs/milestone3/validation-results.md

**Technical subtasks:**
- [ ] Audit tests already written with each layer task; add missing business, repository, endpoint and UI cases specified here.
- [ ] Run the complete positive/negative token and scope matrix plus self/participant/service-principal isolation applicable to this service.
- [ ] Verify persisted outcomes through a fresh context, deterministic test isolation and live frontend/backend acceptance; record actual commands/results without skipping failures.

**Specific acceptance criteria:**
- [ ] Unit-test typed payload validation, defaults, mapping and preference update. Relational-test event/preference uniqueness, retry conflict and persisted upsert values. HTTP-test ingestion credentials, self filtering/403, defaults and pagination. UI-test timeline, preference load/save, empty/error states and no cross-user requests.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run all Notification unit/integration tests and frontend feature tests, then solution regression and architecture checks; record failures/remediation and real human Gate 2 evidence.

**Agent / workflow:** Testing Agent. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F06; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### D01 — Run the six-service application and frontend from a documented local environment

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F07. **Epic:** E00. **Service:** delivery. **Layer:** operations. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P04; [ ] P05; [ ] P07; [ ] S01-T04; [ ] S02-T04; [ ] S03-T04; [ ] S04-T04; [ ] S05-T04; [ ] S06-T04

**User story:** As a teammate or reviewer, I can start the complete system from a clean checkout.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** scripts/start-milestone3.ps1; scripts/stop-milestone3.ps1; infra/compose.yaml; .env.example; docs/milestone3/runbook.md

**Technical subtasks:**
- [ ] Provide a documented startup sequence for SQL Server prerequisites, Keycloak, database migrations, six .NET APIs and React client.
- [ ] Configure ports, CORS, authorities/audiences, service base URLs and machine secrets through environment; local Node frontend only serves client assets.
- [ ] Provide health/readiness diagnostics, safe shutdown and demo-user provisioning instructions; do not include token/password values in source.

**Specific acceptance criteria:**
- [ ] Clean-checkout setup reaches every public /health and renders frontend; services start independently, only Gameplay workflow requires peers. Database/IdP version prerequisites and troubleshooting are explicit. Restart preserves application data, with no automatic destructive reset.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Have another member follow the runbook from a clean environment and record commands, ports and redacted health results.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F07; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### D02 — Migrate legacy data safely and retire the aggregate host and shared context

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F07. **Epic:** E00. **Service:** delivery. **Layer:** data. **Priority:** P1. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] P04; [ ] S01-T02; [ ] S02-T02; [ ] S03-T02; [ ] S04-T02; [ ] S05-T02; [ ] S06-T02; [ ] S01-T04; [ ] S02-T04; [ ] S03-T04; [ ] S04-T04; [ ] S05-T04; [ ] S06-T04

**User story:** As an existing developer with QuizArenaDb data, I need a safe transition to service-owned databases.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** scripts/migrate-legacy-data/; docs/milestone3/data-cutover.md; QuizArena.sln; legacy src/QuizArena.{Api,Data,Domain}/; legacy tests/

**Technical subtasks:**
- [ ] Back up/export legacy data; dry-run field/ID mappings and collision/unmapped-subject report without changing source database.
- [ ] Import owned data idempotently with explicit legacy-Guid-to-provider-subject mapping, string match IDs and safe handling of old mock-question matches; quarantine invalid/incomplete historical records with audit counts.
- [ ] Verify row counts/invariants, retain source for rollback, then remove aggregate runtime/projects/context from solution and fix documentation/test references; retain historical docs marked as historical.

**Specific acceptance criteria:**
- [ ] No silent dropped data, coerced identities, invented provider subjects or password migration into IdP. Invalid mock matches remain archived rather than offered as playable real matches. Rerun import creates no duplicates. Six contexts enforce only their owned schemas; no runtime repository references QuizArenaContext. Backup/rollback is documented and source data deletion needs separate explicit approval.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Dry-run plus disposable-database rehearsal, before/after table counts, idempotent second run and rollback rehearsal; search source/solution for obsolete project/context references.

**Agent / workflow:** Data & Persistence Agent; Testing Agent for tests. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F07; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### D03 — Verify the complete secured two-player journey and delivery retries

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F07. **Epic:** E00. **Service:** delivery. **Layer:** testing. **Priority:** P0. **Estimate:** L. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] D01; [ ] D02; [ ] P10; [ ] S01-T06; [ ] S02-T06; [ ] S03-T06; [ ] S04-T06; [ ] S05-T06; [ ] S06-T06

**User story:** As a reviewer, I need evidence that the six independently running services work together through the browser.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** tests/QuizArena.EndToEndTests/; src/QuizArena.Client/e2e/; docs/milestone3/validation-results.md

**Technical subtasks:**
- [ ] Use Playwright with two authenticated player contexts and real running services/provider to create/join/lock lobby, play five rounds, and view history/leaderboard/events/preferences.
- [ ] Exercise bad/expired tokens, wrong scopes, user spoofing, unauthorized internal endpoints and answer-key leakage; confirm calls use HTTP instead of shared assemblies.
- [ ] Interrupt downstream Statistics/Notification delivery, restart Gameplay, restore peers and verify pending retries produce exactly one result per player and one event per lifecycle recipient; test simultaneous submissions and draw.

**Specific acceptance criteria:**
- [ ] One complete win and one draw journey pass; both players answer once per round, scores persist after restart, unauthorized caller cannot affect another player. Completion data eventually reaches both downstream services, duplicates do not accumulate. No browser internal credentials or answer keys. Record actual test counts, durations, run URLs and failures/remediation.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Run npm run test:e2e with isolated environment and dotnet test QuizArena.sln; archive redacted results/screenshots as generated CI artifacts.

**Agent / workflow:** Testing Agent. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F07; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### D04 — Write the Milestone 3 architecture, security and reflection report

**Type:** Task. **Assignee:** @CarterBeasse. **Suggested peer:** @SuSu-Lisu. **Parent feature:** F07. **Epic:** E00. **Service:** delivery. **Layer:** documentation. **Priority:** P1. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] D03; [ ] P08

**User story:** As an assessor, I can verify the architecture, work allocation and human/AI collaboration evidence.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** docs/milestone3/architecture-and-reflection.md; docs/{architecture-notes,api-service-contracts,database-schema}.md; docs/milestone3/{authentication,authorization-matrix,work-allocation,validation-results}.md

**Technical subtasks:**
- [ ] Update current architecture/service/data/API docs while marking Milestone2 plans/review counts as historical; link independently runnable projects and React integration.
- [ ] Write a concise 2–3-page equivalent report covering layering, frontend choices, two services/member, agent assistance/manual corrections and defects caught by human gates.
- [ ] Include OAuth actors/PKCE flow, redacted token settings, full endpoint-policy mapping, actual security tests, board/PR/plan/review links and GitHub-over-Azure submission decision.

**Specific acceptance criteria:**
- [ ] Report is in the existing lowercase docs folder, linked from root README rather than creating a competing Docs tree; its rendered length is checked for 2–3 pages. Evidence is real, no fabricated AI corrections or signoffs. SQL ownership, no direct frontend storage and no cross-service assembly references are accurate. All required deliverable URLs are collected.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Peer-read report against rubric; validate links, endpoint names and current solution. Render/check page count or document the Markdown-to-PDF rendering settings used.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F07; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### D05 — Audit issue traceability, acceptance evidence and board completeness

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F07. **Epic:** E00. **Service:** delivery. **Layer:** process. **Priority:** P1. **Estimate:** S. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] D03; [ ] D04

**User story:** As the milestone owner, I need every requirement traceable to completed, reviewed work.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** docs/milestone3/requirements-traceability.md; GitHub Project; epic/feature/task issues

**Technical subtasks:**
- [ ] Map every numbered rubric section and deliverable to actual feature/task numbers, PRs, tests and docs.
- [ ] Audit each member's two complete vertical slices, all frontend tasks, dependencies and issue/PR references.
- [ ] Publish board export or durable evidence and reconcile statuses with actual human-gate completion, leaving pending items open.

**Specific acceptance criteria:**
- [ ] No orphan task, missing owner, missing frontend/test task or false Done status. Every required deliverable has a real URL. Blocked/remaining work is named explicitly; completion requires both acceptance and human gates.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Machine-readable issue map/export counts match epic/feature checklists; manually follow one full trace per member.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F07; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### D06 — Complete read-only review and human signoff for the milestone candidate

**Type:** Task. **Assignee:** @SuSu-Lisu. **Suggested peer:** @JimRashed. **Parent feature:** F07. **Epic:** E00. **Service:** delivery. **Layer:** review. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] D03; [ ] D04; [ ] D05

**User story:** As a peer reviewer, I need confidence in security, layering and test quality before merging.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** docs/milestone3/review-findings.md; milestone PR checklist/signoff log

**Technical subtasks:**
- [ ] Run the constrained Code Review agent read-only over the complete milestone diff; capture category/severity/file/action in the established format.
- [ ] Owners resolve all critical/high findings through linked issues/PRs and rerun affected checks; justify any lower-risk deferral with a concrete follow-up issue.
- [ ] Have at least one other human member review each PR, verify all three gates/security checklist and record dated signoff with identity and CI evidence.

**Specific acceptance criteria:**
- [ ] Automated review does not count as human approval. No unresolved critical/high findings; missing real approval remains blocked. Every service/feature has owner plan approval, post-implementation inspection and independent pre-merge review. Review agent modifies no files.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Inspect review records, latest successful CI and actual GitHub approvals; reviewer must differ from author; remediate stale approvals after substantive changes.

**Agent / workflow:** Code Review Agent (read-only) and human reviewers. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F07; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### D07 — Merge the retained milestone branch through a reviewed GitHub milestone PR

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F07. **Epic:** E00. **Service:** delivery. **Layer:** release. **Priority:** P0. **Estimate:** M. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] D06

**User story:** As the milestone owner, I need one reviewed submission PR covering all Milestone 3 work.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** GitHub milestone-level PR; docs/milestone3/submission.md

**Technical subtasks:**
- [ ] Verify all milestone changes are on feature/milestone-3-layered-service-development; update from main without destructive reset and resolve conflicts through review.
- [ ] On first push only use git push -u origin feature/milestone-3-layered-service-development; open GitHub PR to main with epic/feature/task links, Milestone 3 title, summary, test results and human signoff log.
- [ ] Obtain real peer approval and passing checks, merge according to repository rules, disable automatic deletion for this branch and verify the milestone branch remains after merge.

**Specific acceptance criteria:**
- [ ] No direct push to main, no fabricated approvals, no premature merge. One milestone-level GitHub PR exists even when task PRs already exist. Source instructions' Azure DevOps destination is overridden by explicit user choice. Retain branch after merge and preserve all traceability links.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Record PR URL, base/head, merged commit, latest CI and independent approval; verify remote milestone branch still exists.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F07; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

### D08 — Publish the final GitHub milestone submission bundle and board export

**Type:** Task. **Assignee:** @JimRashed. **Suggested peer:** @CarterBeasse. **Parent feature:** F07. **Epic:** E00. **Service:** delivery. **Layer:** documentation. **Priority:** P1. **Estimate:** S. **Milestone:** Milestone 3 — Layered Service Development.

**Start prerequisites:** [ ] D07

**User story:** As the submitter, I need the exact final evidence links ready for course submission.

**Current baseline / sources:** See verified baseline, repository source-document list and the linked prerequisite outputs.

**Target files / assets:** docs/milestone3/submission.md; GitHub Project export/evidence; epic completion comment

**Technical subtasks:**
- [ ] Collect repository URL, board URL/export, merged task/milestone GitHub PRs, architecture/reflection report and validation/security evidence.
- [ ] Confirm every feature/task meets closure criteria and update epic checklists/statuses truthfully.
- [ ] Prepare the merged GitHub milestone PR URL for Lea submission; the human submitter performs course-system submission unless separately authorized.

**Specific acceptance criteria:**
- [ ] Bundle contains all rubric deliverables using GitHub equivalents. No Azure DevOps setup task is created. Board/issue states reflect real completion; retained branch and milestone PR are verifiable. Lea submission is not falsely claimed.
- [ ] The specified file ownership, layer boundaries and scope policies are verified for all changed paths; relevant backend/frontend behavior is demonstrated using the validation below.
- [ ] Actual plan, validation evidence, read-only review and independent human approval are linked under the three gates; the issue is not closed merely on code generation.

**Validation / evidence required:** Open every final link as a reviewer and verify report/board evidence remain accessible; owner acknowledges submission bundle.

**Agent / workflow:** Prime/Plan for research; appropriate scoped layer agent only after Gate 1. Follow Research -> Plan -> owner Gate 1 -> scoped implementation with tests -> owner Gate 2 -> read-only review + independent peer Gate 3. Save issue-scoped plan under specs/milestone3/<actual-issue-number>-plan.md. Link all commits and PRs to this task and F07; task PRs target the milestone branch.

**Scope limits:** Implement only this task's approved changes and explicitly necessary tests. Other layer work belongs to its linked task/agent. No credential values, invented test results or invented human signoffs. No unrelated feature expansion. Add the shared definition of done above verbatim to the published issue.

## Dependency and rubric audit before returning

Verify the graph contains no cycles. Major start path: P01 -> P02 -> P03/P05 -> P04/P06 -> service domain/data/application/API tasks; P07 unlocks parallel frontend work; P08 configures layer agents; P09/P10 supply test/CI gates. Gameplay application additionally waits for QuestionBank and Lobby internal APIs. Feature tests wait for live owned API + frontend. Delivery sequence D01/D02 -> D03 -> D04 -> D05 -> D06 -> D07 -> D08, with the explicit dependencies in each body taking precedence over this summary.

| Source requirement | Coverage |
|---|---|
| 1 Layering, six independent hosts, client isolation | P02–P04, P07, every Sxx-T01..T05, D01–D02 |
| 2 OAuth/OIDC, validation, scope policies, tests/docs | P05–P06, P09, every Sxx-T04/T06, D03–D04 |
| 3 Parent work items, 3–6 technical subtasks, AC, owner | E00, F00–F07, every task body and Project metadata |
| 4 Two services per member, contract-first, references | P01–P02, each service feature, D05 |
| 5 Layer agents plus testing/review skill | P08 |
| 6 R-PIV, approved file plan, scoped agents, checks | common definition of done, P08–P10, D06 |
| 7 Three human gates, real peer approval/security review | common definition of done, D06–D07 |
| Board, repository, merged PR evidence | P01, D05, D07–D08 |
| 2–3-page architecture/reflection document | D04 |
| Milestone branch kept; milestone PR submitted | P01, D07–D08; GitHub explicitly replaces Azure DevOps |

If any existing issue is reused, preserve its real GitHub number and history in the output map. Never create extra duplicate task issues merely to make the newly-created count equal 63. The planned total is 63 distinct scopes; actual creation count may be lower due to reuse. Do not mark a newly published backlog complete or imply the software has been implemented.

