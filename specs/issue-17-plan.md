# Issue #17 (P02): service boundaries and public contracts

Status: implementation, independent Testing, Validate and read-only Review PASS after two repair cycles on 2026-11-03. Human architecture approval is pending from JimRashed, CarterBeasse and SuSu-Lisu. No human approval is implied by this document.

Issue: https://github.com/JimRashed/Web-3-Final-Project/issues/17
Parent: #8 (F00). Epic: #7 (E00).
Base: `feature/milestone-3-layered-service-development` at `4917c48`.
Issue branch: `feat/17-p02-service-contracts`.

## Summary and architecture

Produce the proposed contract package needed before six independently runnable services and their React consumers are implemented. This change documents intended behavior; it does not claim the existing modular monolith already implements it. Preserve the historical baseline documents and production code.

Research verified one API host, one shared QuizArenaContext, sixteen baseline operations, mixed GUID/string identifiers, DTOs in Domain, and mock/in-process Gameplay integrations. Dispatcher verified #16 completed, #17 reopened and Ready, no open PR, and a clean isolated worktree. Dispatcher also compared all 54 live leaf issues #16–69 against the local manifest: stable IDs, acceptance-criterion text and all technical steps match 54/54. The local milestone prompt and manifest are therefore verified scope inputs for this run; record this dated comparison in final validation evidence.

The target has one Domain/Data/Api triple and SQL Server database per service. Domain owns pure rules/entities and persistence abstractions; Data references only its own Domain; Api references its own Domain/Data and owns HTTP DTOs, application interfaces/orchestration and typed peer HTTP adapters. No cross-service assembly references, shared tables, foreign keys or direct database queries. React uses public HTTP DTOs and IdP login; no MongoDB or Express business API is required.

Current repository dispatcher policy supersedes historical intermediate review gates: stages proceed automatically and final PR gathers the human architecture/security/acceptance approvals. Preserve historical `specs/implementation-plan.md`. This plan uses the current prescribed `specs/issue-17-plan.md` path; the final evidence document must explain this mapping from the older issue-body `specs/milestone3/17-plan.md` convention.

## File inventory

Create only the following issue-scoped files (plus ignored dispatcher reports):

| File | Purpose |
|---|---|
| `specs/issue-17-plan.md` | This plan, updated only for implementation decisions/evidence links |
| `docs/milestone3/architecture-decisions.md` | Proposed decisions, owners, layering/table ownership, deployment URLs, interaction/transaction boundaries, version transition and pending team signoffs |
| `docs/milestone3/api-contracts.md` | Complete route/schema/error/idempotency/pagination contract, application interfaces, frontend route map and leaf traceability |
| `docs/milestone3/authorization-matrix.md` | Every route's policy, audience, actor kind, scope and ownership rule plus security examples |
| `docs/milestone3/identity-migration.md` | Verified legacy account mapping, password retirement, data preservation, reconciliation, quarantine and rollback |
| `docs/milestone3/p02-validation.md` | Exact commands/results, operation/example checks, read-only review findings and pending human signoff record |
| `contracts/openapi/identity.yaml` | Identity API proposal |
| `contracts/openapi/statistics.yaml` | Statistics API proposal |
| `contracts/openapi/questionbank.yaml` | QuestionBank API proposal |
| `contracts/openapi/lobby.yaml` | Lobby API proposal |
| `contracts/openapi/gameplay.yaml` | Gameplay API proposal |
| `contracts/openapi/notification.yaml` | Notification API proposal |
| `contracts/validate-contracts.mjs` | Dependency-free, read-only structural/semantic contract checks and schema-example checks |

No source, solution, project, migration, existing test project or frontend implementation changes are planned. No new package manifest/dependency installation is required. Validation tooling is scoped to real contract invariants, not assertions that merely mirror prose.

## Ordered implementation steps

### 1. Freeze the proposed decisions and ownership

In `architecture-decisions.md`, clearly label every design decision Proposed pending final team approval. Assign Identity/Statistics to CarterBeasse, QuestionBank/Lobby to JimRashed, Gameplay/Notification to SuSu-Lisu. Document owned source/test/frontend paths and every existing table's destination. Keep QuizArenaDb intact until verified migration.

Set configurable development origins: React5173; Keycloak8080 `/realms/quizarena`; Identity5101, Statistics5102, QuestionBank5103, Lobby5104, Gameplay5105, Notification5106. Local HTTP/authority metadata only in Development; deployed HTTPS. Swagger Development-only and CORS restricted to configured client origin.

Specify Keycloak authorization-code PKCE S256 public client `quizarena-client`, tokens in memory, no client secret/password grant. Use exact issuer plus opaque subject, unique Identity `(Issuer, Subject)`, one configured trusted issuer per deployment. APIs independently validate issuer/audience/signature/lifetime. Explain how issuer is implicit in user-facing `userId` paths but preserved in internal ownership keys and migration mappings.

Completion check: each service has one human owner, table/path ownership, target origin and deployment/security boundary; historical current behavior is distinguished from target behavior.

### 2. Specify the complete operation inventory and schemas

Create six self-contained OpenAPI 3.0.3 documents with unique operationIds, tags, servers, parameters, explicit success/error responses, reusable local schemas, examples and security. Use JSON-compatible YAML syntax so Node's built-in JSON parser can validate the files without installing packages; document this intentional representation. JSON is a YAML subset. Describe the validator's supported subset and avoid claiming a complete OpenAPI conformance certification.

Preserve the following exact leaf scopes:

| Service | Operations and primary outcomes |
|---|---|
| Identity | GET `/api/users/me`200; PUT same with `{displayName}`200; POST `/api/auth/register`, POST `/api/auth/login`, GET `/api/users/{id}`410 |
| Statistics | POST `/api/results`200 empty for first/identical replay,409 changed replay; GET `/api/players/{userId}/stats`200; GET `/api/players/{userId}/matches`200 array; GET `/api/leaderboard`200 array |
| QuestionBank | GET `/api/questions/generate`200 safe array, count1..50/default10, empty catalog `[]`; POST `/internal/question-sets` with `{count:5}`200 exact five snapshots or409 insufficient catalog |
| Lobby | POST `/api/lobbies` with `{}`201 and Location; GET `/api/lobbies/{joinCode}`200 safe preview; POST `.../join`200; POST `.../lock`200; GET `/internal/lobbies/{joinCode}`200 |
| Gameplay | POST `/matches` with `{joinCode}`201 first/200 existing for same stable lobby; GET `/matches/{matchId}`200; GET `.../next-question`200 or409 finished; POST `.../answers` with `{questionId,answer}`200 |
| Notification | POST `/events`202 persisted response, identical replay same response,409 changed replay; GET `/events`200 self array; GET `/users/{userId}/preferences`200 defaults/stored; PUT same200 |
| All six | GET `/health`200 public safe status |

Use camelCase JSON, UTC date-time fields, explicit nullable fields and requiredness. Specify practical field bounds consistently in prose/schemas/examples. Use `additionalProperties:false` on user mutation inputs to reject client-selected current-user/host/player IDs; retired password routes ignore bodies and never bind/log/store submitted credentials. Public retired endpoints return safe410 OIDC guidance even without a token, consistently in all documents.

Question public schema has id(Guid), prompt, category and four distinct nonblank options. Internal snapshot alone includes correctAnswer matching exactly one option. Public schema/reference closures and public examples must not contain answer keys. Internal operations are excluded from browser client generation and browser Swagger examples; keep controlled internal contract documentation distinct.

Preserve legacy MatchId strings (maximum100 as current storage) while new Gameplay generates UUID-formatted strings. Do not impose a UUID-only schema that rejects retained historical values. New result scores are0..5; legacy historical score responses can exceed5. Persist original PlayedAt in immutable retry payloads.

Pagination stays arrays, with page>=1/default1, pageSize1..100/default20, newest-first stable tie-breaks (history PlayedAt then MatchId; events CreatedAt then EventId). Leaderboard top1..100/default10, totalScore descending then wins descending then userId ordinal. Optional pagination headers must not silently alter bodies.

Completion check: 30 operations total (5 Identity +4 Statistics +2 QuestionBank +5 Lobby +4 Gameplay +4 Notification +6 health), with complete path parameters, schemas and examples; every referenced schema resolves locally.

### 3. Define lifecycle, interservice delivery and frontend contracts

In architecture/API documents, define Lobby's six uppercase alphanumeric join codes, Open->Locked state, two distinct subjects including host, idempotent repeated joins while Open, capacity/Locked409, and repeated host lock returning current state. Nonmember preview exposes safe lobby state/member count, not other players' subject IDs; machine lookup includes stable lobby ID, host, two subjects and state.

Gameplay creation verifies caller is locked lobby host, gets exact five valid immutable QuestionBank snapshots, and commits one match per stable lobby ID under a concurrency-safe unique constraint. No partial match on dependency503. Define first/replayed creation response and fresh authorization of replay callers. GET never advances rounds; both participants answer once per current round; stale/repeated/finished submissions409, outsider403; five rounds, one point per correct answer, draw with no winner. Return the agreed answer outcome fields without revealing the answer key.

Gameplay persists match completion and four outbound delivery records atomically (two Statistics results and two completion events). Creation events also persist with creation. Persist immutable payloads and eventIds before network delivery, independent per-destination progress, timeouts, bounded exponential backoff with jitter, restart redrive and visible permanent failures. Define transient retries for network/408/429/5xx, honor bounded Retry-After, and mark unfixable4xx terminal/manual redrive. No distributed transaction or claim of exactly-once transport; receiver idempotency provides duplicate-safe effects. Results key `(matchId,userId)`; events key eventId; equal replays return original success and changed payloads409.

Notification schemas use typed MATCH_CREATED/MATCH_COMPLETED payloads and preserve optional winner/draw semantics. Preference defaults false/false; GET does not persist defaults. Email/push delivery remains excluded.

Define each service's application interface operations (Async suffix, CancellationToken, pure domain arguments versus HTTP DTO mapping) and typed peer adapter boundaries. Map React routes `/profile`, `/statistics`, `/leaderboard`, `/questions`, `/lobbies`, `/matches/:matchId`, `/notifications`, `/preferences` plus OIDC callback/session handling to owning feature paths, operations and loading/empty/error/forbidden states. UI never calls internal machine routes, chooses current-user IDs or renders answer keys.

Completion check: creation, scoring, completion, retries, concurrent duplicate requests and UI read/write paths are deterministic without downstream implementers inventing behavior.

### 4. Complete authorization and negative examples

Map every operation to public/user/machine access and audience `quizarena-<service>`. User scopes: profiles.read/write; stats.read,leaderboard.read; questions.read; lobbies.read/write; matches.read/write; events.read,preferences.read/write. Machine scopes only lobbies.verify,questions.play,results.write,events.write. Require exact scope tokens AND allowlisted machine client identity for internal writes; user policies reject machine tokens, even unexpected user scopes. State the configured client identity claim (`azp` for the proposed Keycloak setup) and trusted issuer configuration rather than trusting arbitrary claims.

Document invalid/missing token401 with challenge, valid insufficient scope/ownership403, invalid input400, missing404, conflict409, unavailable dependency503, safe500; cancellation is not generic500. Define whether existence/ownership is checked first so behavior does not leak unauthorized records. Add positive and negative examples covering every operation (including health/retired endpoints) and ID spoofing, wrong audience/issuer, user-to-machine access, machine-to-user access, duplicate conflicts and key leakage.

Completion check: route count and scope/audience/type/ownership policies agree across API prose, matrix and OpenAPI extensions/security declarations.

### 5. Specify legacy identity and data migration

Define an explicit reviewed mapping ledger from each legacy GUID/string user reference to verified issuer/subject, with evidence and mapping provenance. No automatic linking based only on username/email; unverified, ambiguous or missing matches are quarantined. Preserve legacy keys for reconciliation and map all service-owned copies consistently without cross-service FKs.

Document IdP registration/recovery/password reset transition: no password/hash import assumption, no reuse of hand-built JWTs, no exposing credentials. Inventory/snapshot/backup source database, generate mappings, import into separate target service databases idempotently, verify row counts, identity links, aggregate totals, match/event IDs and original timestamps, and only then switch traffic. Explain no dual-write ambiguity, freeze/cutover ordering and rollback before/after target writes. Active matches/lobbies require explicit drain/archive; retain unknown historical MatchIds and score totals without silent truncation. Supply synthetic mapping examples only, not production identity data.

Completion check: legacy endpoints/identifiers/password behavior each have an explicit replacement/retirement and rollback path with no destructive migration code in this issue.

### 6. Validate, trace and hand off for review

Add `contracts/validate-contracts.mjs` using Node built-ins only. It must fail nonzero for malformed document JSON/YAML subset, missing documents, dangling refs, duplicate operationIds, undeclared path parameters, missing success/error schema/security metadata and public answer-key leakage. Validate examples against the schema features actually used: type, required, additionalProperties, enum, nullable, string length/pattern/format, numeric bounds, arrays/items/min/max/uniqueItems and refs/compositions if used. Fail unsupported assertion keywords rather than silently declaring examples valid. Treat relational invariants (correct answer equals an option, aggregate outcomes and identity ownership) as explicit semantic checks/manual evidence, not generic JSON Schema guarantees.

Require all 30 expected operations and meaningful security/route consistency checks. Record per-file/operation/example counts and failure details. Include deliberate in-memory negative probes (without editing committed documents) to establish that broken refs, missing security, invalid examples and public key exposure are detected. No superficial tests of Markdown phrasing.

Write `p02-validation.md` with actual tool versions, commands, results, limitations, local/live leaf-scope comparison, changed-path ownership, and review findings. Include a traceability matrix for P02 subtasks/acceptance and S01..S06 Domain/Data/API/HTTP/UI/testing scopes, plus platform consumers P03..P10 and delivery requirements. Human approval rows for all three members remain Pending with blank evidence until real reviews exist. Link this plan and final PR instead of inventing Gate1/2/3 signoffs.

## Acceptance coverage

| Requirement | Planned steps/evidence |
|---|---|
| Proposed architecture/product choices reviewed, complete six-service ownership and boundaries | 1,3; architecture decision IDs and final named approval rows |
| Schemas, endpoint/status/policy matrix, URLs, errors, pagination and idempotency before fan-out | 1–4; six OpenAPI files, route crosswalk and contract checker |
| Legacy identity mapping, machine identities/scopes/audiences, delivery/version transition | 1,3–5; migration ledger specification and delivery examples |
| Six service contracts plus explicit frontend routes, no answer-key/current-user spoofing | 2–4; public-schema closure checks, strict mutation inputs, UI mapping |
| DTOs/interfaces in own Api, pure Domain, no shared assemblies/tables | 1,3; dependency and owned-path/table matrix |
| Existing endpoint/ID/account/password migration documented | 2,5; all16 baseline routes mapped to target/retired behavior |
| Ownership/scope policies verified for changed paths and relevant behavior demonstrated | 6; file inventory and executable contract/example validation; runtime behavior explicitly future work |
| Plan, validation, read-only review, independent human approval linked | 6; evidence doc and draft PR; human approval remains a final outstanding action |
| All three members review contracts; all leaf scopes compared; every route classified | 4,6; full operation/leaf crosswalk and pending team approval register |

## Validation commands and execution boundaries

From worktree root:

```powershell
node --version
node contracts/validate-contracts.mjs
git diff --check
git status --short
```

Implement should run a baseline build per its role. Required .NET commands when reporting regression execution are:

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror -m:1 /nodeReuse:false
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal
```

Document-only acceptance is contract validation and read-only review; do not require invented runtime tests for endpoints that do not exist yet. If the existing build/test commands are run, record exact results and distinguish baseline failures from this change. Integration tests require SQL Server LocalDB; frontend scripts cannot run because the client project does not yet exist. Do not claim unit/integration/E2E coverage of the proposed six-service system. Keep raw logs ignored under `.dispatcher/`.

Planning environment inspection found no existing package manifest/OpenAPI validator and no resolvable Node `yaml`, `js-yaml`, `ajv` or `@apidevtools/swagger-parser`. The `python` command resolves to an inaccessible WindowsApps stub. Therefore the dependency-free JSON-subset YAML and limited semantic checker are deliberate choices; full standards conformance validation remains a clearly stated limitation unless a real validator is later available.

## Risks and exclusions

- Architecture decisions and all three members' approvals remain pending until final review; do not close #17 just because documents exist.
- Live leaf-scope comparison passed 54/54 during planning; recheck issue/PR eligibility before publication. Preserve the distinction between this verified snapshot and future remote changes.
- Schema validation cannot prove database isolation, OAuth enforcement or distributed retry correctness. Those require the implementation issues' integration/security tests.
- No provider provisioning, service split, database migration execution, frontend code, CI redesign, broker, SignalR, matchmaking, catalog administration, email/push transport or extra business endpoint is included.
- Historical documentation remains a baseline; target documents must identify themselves clearly so readers do not mistake proposals for deployed behavior.
- Dispatcher owns the conventional commit, push and draft PR against the milestone branch, referencing #17, #8 and #7. Workers do not publish. Default repair budget is two additional cycles shared across validation and review.
