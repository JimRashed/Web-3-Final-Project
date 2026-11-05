# Issue #43 — QuestionBank coverage audit

## Summary and architecture

Issue: [#43](https://github.com/JimRashed/Web-3-Final-Project/issues/43), parent feature #11, epic #7. Branch `feature/m3/43-questionbank-coverage` started at milestone commit `a79f9ba90cc1a114019db93200289be3cf6cbeb1` and refreshed to `a5f973f3caa2c79a203db735d98d3d78ff33dbb9` before final validation, preserving the merged Lobby audit and Gameplay/runbook work. Dispatcher verified #41, #42 and #24 closed. This is an agent-reviewed implementation plan; independent human approval remains pending at the final draft PR under the current repository dispatcher policy.

Audit existing coverage and close the real SQL-backed HTTP vertical-slice gap. Existing domain, service, relational, endpoint JWT and React suites already cover most required behavior. Preserve those tests and run them; add cases that connect the production controllers, application service and repository to an isolated migrated SQL database while retaining real JwtBearer validation. Production ownership remains API contracts/orchestration, pure Domain rules and Data persistence. No production behavior, contract, dependency or authorization policy changes are planned.

## File inventory

| File | Action and responsibility |
| --- | --- |
| `specs/issue-43-plan.md` | Create this scoped plan; update only for evidence-supported implementation adjustments. |
| `tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/QuestionBankSqlApiFixture.cs` | Create isolated SQL-backed real-JWT HTTP fixture and fresh-context catalog helpers. |
| `tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/QuestionSqlEndpointsTests.cs` | Create positive public/internal routes and exact-count shortage/read-immutability tests. |
| `tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/QuestionSqlPrincipalIsolationTests.cs` | Create complementary actual-route principal/lifetime denial cases with SQL non-mutation and answer non-disclosure assertions. |
| `docs/milestone3/validation-results.md` | Append a clearly marked #43 acceptance audit, actual commands/results, review status and pending live/human checklist; preserve historical evidence. |

No existing tests are deleted, disabled or weakened. No frontend changes are planned because existing feature, lifecycle and API tests already address generation, bounds, errors and answer secrecy. Testing may identify a concrete missing case; an exact file-inventory adjustment must precede any additional file change.

## Ordered steps

1. **Establish the baseline and audit.** Run a warnings-as-errors solution build and focused QuestionBank unit/integration and frontend feature/API tests before edits. Inspect existing domain, application, repository, endpoint, security and UI cases. Record actual totals and failures separately from subsequent results. Completion: each acceptance row below has concrete existing or new test evidence.
2. **Create the SQL HTTP fixture.** Reuse `Security/SecuredApiFactory` ephemeral signing metadata and production authorization pipeline. Override only `QuestionBankDbContext` registration/options with a unique `QuizArenaQuestionBank_Http_<guid>` database, preserving the production `IQuestionRepository`, `IQuestionCatalogService` and controllers. Respect `QUIZARENA_TEST_SQLSERVER`, otherwise existing LocalDB conventions. Migrate and seed the owned database, create HTTPS test clients, expose independent context reads, and dispose client/factories before deleting only that generated database. Guard database ownership before deletion. Completion: a test can request the actual route and independently read its catalog from a fresh context without any repository/service double.
3. **Verify positive SQL-backed HTTP behavior.** Public requests at count1, default10 and count50 return distinct available IDs with exactly the public field set and no `correctAnswer`; match prompt/category/ordered options against fresh persisted records by ID. Authorized Gameplay machine request returns exactly five distinct snapshots with correct answer material matching persisted values. Compare by identity/set, never random order. Capture complete catalog state through fresh contexts before and after reads and assert equivalence. Completion: meaningful happy-path tests pass through production persistence and mapping.
4. **Verify shortage semantics and principal isolation.** Reduce only an owned test database to four questions. Public count50 returns four safe records; internal exact-five request returns safe409 and leaves the complete persisted catalog unchanged. Add browser-to-internal, machine-to-public and wrong-client-with-correct-scope denials. Add missing-expiry401 and empty/whitespace-subject403 on both actual routes, extending rather than reproducing the existing token matrix. Missing `azp` or ordinal actor/client mismatch may be included when they close a verified actual-route gap. Denied bodies must expose no answer data and fresh-context catalog state must remain unchanged. Completion: expected401/403/409 and safe responses, with no SQL mutation.
5. **Testing audit and complete validation.** Testing independently inspects additions and existing coverage. Run focused suites, then every applicable full gate below with raw local evidence. Append measured results to the validation report, clearly separating pre-existing historical observations. Attempt current provider/API/frontend availability and supported live acceptance. If unavailable, record exact failure and pending real-browser checklist rather than substitute fixture/double evidence. Completion: all automated checks pass or a concrete blocker/failure is returned; no manufactured live results.
6. **Read-only review and final handoff.** Supply complete tracked/untracked diff, plan, coverage audit and current validation evidence to read-only review. Resolve actionable failures through the Dispatcher's shared two-additional-repair budget and revalidate after source/test changes. Dispatcher alone checks exact paths/secrets/scope, commits, pushes and creates a draft PR against the milestone branch referencing #43/#11/#7. Completion: verified draft PR with independent human signoff and live acceptance explicitly pending; no merge or manual issue closure.

## Acceptance coverage

| Issue requirement | Evidence and plan step |
| --- | --- |
| Option/answer invariants | Existing `UnitTests/Domain/QuestionTests.cs` invalid/null/blank/length/distinctness/answer-membership/immutability cases; audit and execute steps1,5. |
| Count bounds, no duplicate selection, answer-free DTO mapping | Existing `Domain/QuestionSelectionTests.cs`, `Services/QuestionCatalogServiceTests.cs`, `Contracts/QuestionContractTests.cs`; SQL-backed route projection/distinctness in step3. |
| Both endpoints, exact-count shortage and service-only answers | Existing `Endpoints/QuestionEndpointsTests.cs`, `QuestionInputTests.cs`, `QuestionErrorTests.cs`, `QuestionContractTests.cs`; production SQL positive/shortage paths steps3–4. |
| No `correctAnswer` in public JSON | Exact public field assertions and persisted-value comparisons step3, existing public mapping/API/UI secrecy tests executed step5. |
| Complete applicable token/scope and principal isolation matrix | Existing actual-route `QuestionRouteSecurityTests.cs` plus `Security/SecurityMatrixTests.cs`; complementary actual-route expiry/subject/client and SQL non-mutation checks step4. |
| Fresh-context persisted outcomes and deterministic isolation | Unique migrated database ownership and teardown step2; complete catalog before/after assertions steps3–4; existing relational persistence/constraint/concurrency tests step5. |
| UI generation, bounds, errors and answer secrecy | Existing `QuestionBankPage.test.tsx`, `QuestionBankPage.lifecycle.test.tsx`, `src/api/questionbank.test.ts`, executed focused and full in steps1,5. |
| Layer/file ownership and scope policies | Test-only inventory; retained production registrations; approved authentication interpretation below; architecture/contract checks step5 and read-only review step6. |
| Plan, actual validation, read-only review and human approval | This plan plus appended #43 report and final PR step6; agent stages do not imply human approval. |
| Live frontend/backend acceptance | Current availability probes and supported live attempt step5; record unmet provider/browser prerequisites and pending checklist before closure when unavailable. |

QuestionBank exposes no player-owned mutable resource: applicable principal separation is public user versus private machine answer access, not an invented self/participant ownership rule. Later approved `docs/milestone3/authentication.md` defines exact signed `qa_actor`, configured `azp` and exact scope plus nonblank opaque subject. Follow that design; do not introduce subject-prefix or service-account subject allowlist policy from older prose.

## Testing strategy

Use existing xUnit/FluentAssertions conventions, descriptive `MethodName_Scenario_ExpectedBehavior` names and Arrange/Act/Assert sections. Unit suites remain free of SQL/network I/O; new SQL tests belong in the existing integration project and use actual bearer verification without an authentication bypass. Keep each scenario focused; full state assertions verify read immutability and non-disclosure rather than relying only on status or row count. Do not assert random distribution/order. Snapshot responses are returned values, not persisted match records. No new framework, dependencies or generic E2E infrastructure are needed.

## Validation commands and prerequisites

From repository root, save actual exit codes, totals, warnings/failures/skips and logs under ignored `.dispatcher/runs/43-20261104/`. SQL Server/LocalDB must be provisioned. `.NET 10` and Node/npm from the locked frontend project are required. Existing cache is an operational restore source, not test evidence. Named-pipe permissions may require approved normal-environment execution.

```powershell
dotnet restore QuizArena.sln --source C:\Users\kilda\.nuget\packages --disable-parallel -m:1
dotnet build QuizArena.sln --no-restore -warnaserror -m:1 --verbosity minimal
dotnet format QuizArena.sln --verify-no-changes --no-restore --verbosity minimal
dotnet test tests/QuizArena.QuestionBank.UnitTests/QuizArena.QuestionBank.UnitTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.QuestionBank.IntegrationTests/QuizArena.QuestionBank.IntegrationTests.csproj --no-build --verbosity normal
dotnet test QuizArena.sln --no-build -m:1 --verbosity normal --logger "trx;LogFilePrefix=regression" --results-directory .dispatcher/runs/43-20261104/regression-results
dotnet test QuizArena.sln --no-build -m:1 --verbosity normal --collect:"XPlat Code Coverage" --logger "trx;LogFilePrefix=coverage" --results-directory .dispatcher/runs/43-20261104/coverage-results
node contracts/validate-contracts.mjs
git diff --check
```

Discover and execute the existing architecture test project with its actual path. Count all regression and separate coverage TRX assemblies, including failed/skipped outcomes; coverage artifacts do not establish an unmeasured percentage.

From `src/QuizArena.Client`:

```text
npm ci --no-audit --no-fund
npm run typecheck
npm run lint
npm run test -- --run src/features/questionbank src/api/questionbank.test.ts
npm run test -- --run
npm run build
```

The current package has no `test:e2e` script. Check current provider discovery, QuestionBank health and frontend availability using configured URLs; a successful probe alone is not live acceptance. A real provider-backed user should generate quizzes at supported bounds, see no answer material, and observe safe errors. Machine-only answer access should be verified with an authorized configured service client without publishing credentials. Record actual live evidence if available, otherwise the precise pending provider/API/frontend/browser conditions. Historical report claims are not new #43 evidence.

## Risks and out of scope

- SQL fixture must override all competing context options safely and guard its generated database name before teardown; never reset or delete caller/shared databases.
- Full-suite failures or a discovered production defect return to Dispatcher; do not weaken authorization or alter policy to force green.
- Preserve existing historical validation content and issue plans. Scope excludes product expansion, Gameplay persistence, random-selection redesign, new auth policy, dependencies and unrelated changes.
- Independent human review, real acceptance and applicable CI remain required before Done/closure. Agents never invent signoffs, approve themselves, merge or manually close the issue. Project status is unavailable with the current token scope.
