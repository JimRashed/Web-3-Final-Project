# Issue #40 — QuestionBank API DTOs and application orchestration

## Summary and architecture

Issue: https://github.com/JimRashed/Web-3-Final-Project/issues/40 (parent #11, epic #7).

Add the owned Api contracts and `IQuestionCatalogService` application seam so authorized players can receive answer-safe catalog selections and authorized service callers can obtain exactly five answer-bearing snapshots. Existing Domain models, selection rules and repository ports remain pure; existing Data implements storage/selection. Api owns DTO mapping, trusted actor checks and orchestration. No service references another service's assemblies or tables.

Research and plan are agent-reviewed; no human approval is claimed. Dispatcher final-only human review policy supersedes historical intermediate gates. Human signoff remains pending at the draft PR. Prerequisites #38, #39 and #17 are verified closed/completed. Base is `feature/milestone-3-layered-service-development` at `b4d672e65b37e0fa89d8d78a2b0d48859aac3b2b`.

Follow `docs/milestone3/api-contracts.md`, `authorization-matrix.md` and `contracts/openapi/questionbank.yaml`. The issue-required `IQuestionCatalogService` implements the application seam called `IQuestionApplication` in P02; do not create duplicate interfaces. The required `QuestionSetResponse` names one internal answer-bearing question element, corresponding to OpenAPI `QuestionSnapshot`; return a collection, preserving the specified JSON array rather than adding a wrapper.

## File inventory

| Action | Exact file | Purpose |
|---|---|---|
| Add | `src/QuizArena.QuestionBank.Api/Contracts/QuestionResponse.cs` | Public DTO with only Guid Id, Prompt, Category and string[] Options. |
| Add | `src/QuizArena.QuestionBank.Api/Contracts/QuestionSetRequest.cs` | Internal request with required count, exactly five; reject unknown JSON properties. |
| Add | `src/QuizArena.QuestionBank.Api/Contracts/QuestionSetResponse.cs` | Separate internal snapshot DTO adds CorrectAnswer; no inheritance exposing answers publicly. |
| Add | `src/QuizArena.QuestionBank.Api/Services/IQuestionCatalogService.cs` | Async Generate and CreateSnapshot seam with trusted actor and cancellation last. |
| Add | `src/QuizArena.QuestionBank.Api/Services/QuestionCatalogService.cs` | Constructor-injected own repository, guard clauses and explicit DTO mapping. |
| Modify | `src/QuizArena.QuestionBank.Api/Program.cs` | Register scoped IQuestionCatalogService implementation. |
| Modify | `tests/QuizArena.QuestionBank.UnitTests/QuizArena.QuestionBank.UnitTests.csproj` | Replace sole own-Domain reference with sole own-Api reference; Domain stays transitively available. |
| Add | `tests/QuizArena.QuestionBank.UnitTests/Services/QuestionCatalogServiceTests.cs` | Mocked application behavior, guards, cancellation and failures. |
| Add | `tests/QuizArena.QuestionBank.UnitTests/Contracts/QuestionContractTests.cs` | Actual JSON shapes, request validation and option-copy independence. |
| Add | `specs/issue-40-plan.md` | This issue-scoped plan and acceptance traceability. |

No files are deleted. No production project-reference changes or package additions are planned.

## Ordered steps

1. **Establish baseline.** Restore/build and run existing QuestionBank unit tests before implementation. Record actual outcomes separately from regressions. Inspect existing Identity/Statistics application guard conventions and security contracts. Completion: baseline evidence and no unrelated modifications.
2. **Define Api-owned contracts.** Add separate sealed public/internal DTOs. Public fields are exactly id,prompt,category,options; internal fields add correctAnswer. Use string arrays copied during mapping. Request count must distinguish missing/null from a valid value and validate exactly five; reject unmapped request members as existing contracts do. No identity, answer or client-selected question fields belong in request. Completion: JSON/validation tests establish exact shapes and invalid requests.
3. **Implement application orchestration.** Add `IQuestionCatalogService` and `QuestionCatalogService` using constructor-injected `IQuestionRepository`. Use `GenerateAsync(AuthenticatedActor actor, int count, CancellationToken cancellationToken)` returning a collection of QuestionResponse; public endpoint's default count10 is documented for #41. Require a user actor, invoke `QuestionSelection.ValidateCount` before I/O, forward exact token to repository GenerateAsync, map all returned questions without reselecting/shuffling. Add `CreateSnapshotAsync(AuthenticatedActor machine, QuestionSetRequest request, CancellationToken cancellationToken)` returning a collection of QuestionSetResponse; require machine actor and valid request count5 before GetExactSnapshotsAsync(5,token). Reject null actors/requests, wrong actor kinds and pre-cancelled operations before repository access using established exception conventions. Preserve Domain shortage/count exceptions, dependency exceptions and cancellation without converting them to HTTP errors. Completion: mocked tests verify correct calls/results, no writes and no access for rejected inputs.
4. **Wire scoped DI and test boundary.** Register implementation scoped in Program; change unit project sole reference to own Api so new application tests and existing Domain tests coexist. Completion: solution builds and architecture test verifies own-only references and pure Domain.
5. **Complete focused coverage.** Add deterministic xUnit/FluentAssertions/Moq AAA tests for every changed application method, contract and branch. Confirm public DTO cannot leak an answer, internal mapping includes exact answer, arrays do not alias source options, no writes occur, and repeated read selections do not invent persistence/idempotency promises. Completion: focused suite passes with no failed/skipped tests.
6. **Validate and review.** Execute required full build/format/regression/coverage checks, inspect complete diff and have read-only reviewer compare issue/plan/contracts/current diff. Dispatcher alone commits/pushes issue-specific changes and creates a draft PR with actual evidence, linked plan and human review checklist. Completion: passing actual checks, no unresolved high/critical findings, draft PR with independent reviewer requested; human approval pending.

## Acceptance coverage

| Issue criterion / subtask | Steps and evidence |
|---|---|
| Public QuestionResponse excludes correctAnswer and has Guid id,prompt,category,options:string[] | 2,3,5; serialized property-set test and mapping assertions. |
| Internal QuestionSetResponse exposes answer material only for service callers | 2,3,5; separate DTO, machine-only application guard, exact-five mapping tests; full scope/allowlisted-client HTTP policy enforcement remains existing security and #41 endpoint responsibility. |
| IQuestionCatalogService in Api invokes Domain validation/repository with cancellation | 3,4,5; invalid count/no-I/O tests, token verification, cancellation tests, scoped registration. |
| Api owns request/response DTOs and application interfaces; Domain has no HTTP DTO dependencies | 2,3,4,6; no Domain source change, architecture suite and complete diff inspection. |
| Mapping, validation, dependency failures, trusted identity use; no EF serialization | 3,5; mocked repository returns owned Domain models, explicit DTO copies, actor/request guards, propagated failures. |
| Idempotency and typed adapters when specified | 3,5; read-only catalog selection has no writes or durable idempotency requirement; no peer adapter is specified. No invented behavior. |
| File ownership, layering, scope policies, no cross-service project references | 4,6; own Api test reference only, architecture suite, existing security integration suite retained. |
| Relevant backend/frontend behavior and actual evidence/review/human gates | 1,5,6; full backend checks and read-only review; frontend/HTTP routes unchanged. PR evidence links and pending independent human signoff, never fabricated approval or manual issue closure. |

## Testing strategy

Use existing test projects with descriptive `MethodName_Scenario_ExpectedBehavior` names and Arrange/Act/Assert comments. Unit tests mock IQuestionRepository and perform no database/network/file I/O.

- Public happy path maps every field/options, returns up-to requested number as provided by repository and handles empty catalog; count boundaries1/50 succeed, 0/51 and other invalid counts reject before repository invocation.
- Internal happy path passes exactly5 and maps answer-bearing elements; missing/null/0/4/6/51 count and null request reject before I/O; insufficient catalog Domain exception propagates unchanged.
- Both methods reject null/wrong actor kind before I/O, forward exact CancellationToken, honor pre-cancellation and propagate repository cancellation/dependency errors. Valid actors originate from security's AuthenticatedActor seam, with no identity fields in request.
- Contract tests serialize with web JSON naming and assert exact public/internal property sets and array shape; public JSON has no correctAnswer at any nesting. Request unknown properties and missing count are invalid. Exercise ASP.NET MVC's actual object-model validator with missing/null/non-five and valid-five count, using an ordinary class with property validation metadata to avoid positional-record constructor/property metadata conflicts. Mutating a mapped DTO options array cannot mutate source Domain options or another mapping.
- Verify no AddAsync or unrelated repository operation occurs. Selection randomness/exactness is already covered in Domain/Data suites; preserve rather than duplicate it.
- Existing architecture and security integration suites verify assembly ownership and scope/actor policies. No HTTP route is changed in #40; HTTP endpoint integration coverage/error mapping belongs #41. No frontend changes or frontend command execution is required for this scope.

## Validation commands

Run from repository root using installed .NET10 SDK, with SQL Server LocalDB available for existing relational integration suites. Use serial MSBuild (`-m:1`) where needed for this environment; cached NuGet source is permitted when default network restore is unavailable and must be recorded.

```powershell
dotnet restore QuizArena.sln -m:1
dotnet build QuizArena.sln --no-restore -warnaserror -m:1
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test tests/QuizArena.QuestionBank.UnitTests/QuizArena.QuestionBank.UnitTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj --no-build --verbosity normal
dotnet test QuizArena.sln --no-build -m:1 --verbosity normal
dotnet test QuizArena.sln --no-build -m:1 --collect:"XPlat Code Coverage"
git diff --check
```

Record exact commands, exits, counts, warnings, uncovered criteria and evidence in ignored `.dispatcher/runs/40-20261104/`. Historical counts are not evidence. Runtime approval boundaries still apply.

## Risks and out of scope

- No unresolved requirement blocker. P02 documents retain historical proposal labels; follow current closed-#17 contract without claiming team approval.
- Existing credential cannot inspect ProjectV2 status (missing read:project); dispatcher records issue/branch/PR/activity eligibility instead, without credential changes.
- Actor-kind checks complement endpoint policies; they do not independently validate bearer tokens, scopes or allowlisted clients. #41 must use existing named policies and claims-derived actor construction.
- Keep no answer-bearing internal contract in public/browser DTOs or Swagger examples when #41 adds routes. Do not add controllers, routes, HTTP error mapping, Swagger expansion, peer HTTP clients or frontend changes here.
- No persistence schema/repository changes, Domain/legacy-monolith rewrites, catalog admin, durable snapshot store, idempotency keys or authorization weakening.
- Human signoff and actual independent peer review remain pending at final draft PR; agents do not approve, merge or manually close #40.
