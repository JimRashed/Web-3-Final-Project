# Issue #41 — QuestionBank REST endpoints

Plan status: agent-reviewed; independent human signoff remains pending on the final draft PR.

Issue: [#41](https://github.com/JimRashed/Web-3-Final-Project/issues/41); parent [#11](https://github.com/JimRashed/Web-3-Final-Project/issues/11), epic [#7](https://github.com/JimRashed/Web-3-Final-Project/issues/7).
Branch: `feature/m3/41-questionbank-endpoints`, based on milestone commit `9671eb8c8d171d7f6407817f36a33ef0c2ac0f34`.
Verified prerequisites #40 and #21 are closed/completed. Prime found no existing #41 implementation or open PR. GitHub Project status cannot be inspected with the current credential's missing `read:project` scope.

## Summary and architecture

Expose the existing QuestionBank application service through two thin, separately secured controllers. `GET /api/questions/generate` defaults to 10 questions, accepts counts 1–50, and returns answer-safe previews (including `200 []` for an empty catalog). `POST /internal/question-sets` requires `{ "count": 5 }` and returns an exact five-element answer-bearing snapshot array, or 409 when the valid catalog is insufficient.

Reuse named `questions.read` user and `questions.play` machine policies and `GetAuthenticatedActor()`; derive identity only from validated claims. Authentication/authorization runs before input validation. Keep count/business selection, immutable snapshot materialization and persistence inside the existing application/Domain/Data boundary. Controllers only bind/validate input, obtain the trusted actor, forward cancellation and return typed responses.

The approved P02 contract is `docs/milestone3/api-contracts.md`, its authorization matrix and `contracts/openapi/questionbank.yaml`. Keep routes, security, schemas and listed statuses unchanged. The full static OpenAPI document is backend documentation; do not serve its answer-bearing internal operation to a browser. Exclude the internal controller from API Explorer. No runtime Swagger package is required.

## File inventory

| File | Change |
| --- | --- |
| `src/QuizArena.QuestionBank.Api/Controllers/QuestionsController.cs` | Add public GET action, default count/binding validation, user policy, typed response/error metadata and service delegation. |
| `src/QuizArena.QuestionBank.Api/Controllers/InternalQuestionSetsController.cs` | Add internal POST action, machine policy, existing validated request DTO, typed snapshot response/error metadata and API Explorer exclusion. |
| `src/QuizArena.QuestionBank.Api/Errors/QuestionBankProblems.cs` | Add owned safe ProblemDetails normalization/result factory, following the existing Lobby pattern. |
| `src/QuizArena.QuestionBank.Api/Errors/QuestionBankExceptionHandler.cs` | Map typed invalid count to 400, insufficient catalog to 409, recognized dependency outages to 503 and unexpected failures to safe 500; preserve caller cancellation. |
| `src/QuizArena.QuestionBank.Api/Program.cs` | Register problem customization/handler and safe MVC invalid-model-state response factory; preserve bearer middleware order, scoped service registration and anonymous health. |
| `contracts/openapi/questionbank.yaml` | Replace obsolete proposed/unimplemented description with implemented-surface documentation; explain internal backend-only documentation/API Explorer exclusion. Preserve P02 shapes, examples, policies and statuses. |
| `tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/QuestionBankApiFixture.cs` | Add isolated HTTP test helpers based on existing `Security/SecuredApiFactory.cs` and `TestTokens.cs`; configure service or repository doubles without replacing bearer authentication. Helpers may use `WithWebHostBuilder` rather than modifying the sealed existing factory. |
| `tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/QuestionEndpointsTests.cs` | Add real HTTP success/default/bounds/empty/snapshot/trusted-actor/cancellation cases; include real application service with an own repository double. |
| `tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/QuestionInputTests.cs` | Add malformed query/body, missing/null/unknown fields, exact-five and authentication-before-validation cases. |
| `tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/QuestionErrorTests.cs` | Add typed error/dependency/unexpected safe ProblemDetails and leakage regression cases. |
| `tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/QuestionRouteSecurityTests.cs` | Exercise both production routes with real signed bearer tokens and actor/scope/client failures. |
| `tests/QuizArena.QuestionBank.IntegrationTests/Endpoints/QuestionContractTests.cs` | Compare actual routes/policies/responses with committed OpenAPI, check public fields/examples and API Explorer exclusion, and anonymous health. |
| `specs/issue-41-plan.md` | This scoped plan and any necessary implementation-aligned adjustment. |

No deletion, new package, new project reference, Domain/Data/security-policy change, frontend change, or legacy monolith change is planned. Use local hand-written doubles if a mocking dependency is unavailable in IntegrationTests. Test organization may combine closely related scenarios within the listed files without widening scope.

## Ordered implementation steps

1. **Establish baseline.** Restore/build the solution and run existing QuestionBank unit/integration tests. Save actual output in ignored `.dispatcher/runs/41-20261104/`; report existing failures separately. Inspect the existing service, ordinary `QuestionSetRequest` class, policies and error conventions before editing.
2. **Add owned safe errors.** Implement normalization that emits exactly `type`, `title`, `status`, `detail`, `traceId`, with a safe absolute type URI, generic status-specific descriptions and bounded nonempty trace ID. Remove instance/exception/model-state extensions. Do not echo attempted values, exception/SQL details or answer text. Configure MVC invalid-model responses to use this safe factory, including missing-body/content-type failures normalized to contract 400. Recognize dependency timeout/SQL-unavailability causes, including wrapped causes; do not classify every database/invalid-operation failure as a dependency outage. Let caller cancellation propagate rather than manufacture 500 after disconnection. Verify error HTTP tests and no business rules added to the handler.
3. **Expose production routes.** Add constructor-injected controller actions with existing services and request/response DTOs. Public GET binds a default count 10, validates 1–50 and propagates `HttpContext.RequestAborted`; internal POST uses existing required exact-five request validation and unknown-member rejection. Apply exact named policies, retrieve actors from claims and return arrays with 200. Do not introduce a caller-supplied identity or return EF/Domain entities. Add explicit `ProducesResponseType`/content metadata matching the approved contract. Internal controller has `[ApiExplorerSettings(IgnoreApi = true)]`. Both operations return 200; no Location header is applicable.
4. **Prove behavior over HTTP.** Add test helpers with fresh state and the existing real JwtBearer/OIDC metadata fixture. Use service doubles to prove boundary delegation, failures and cancellation, and at least one real application-service/repository-double flow to prove public mapping, exact snapshots, empty and insufficient-catalog behavior without duplicating #39 relational tests. Keep actual bearer handler active for every security scenario.
5. **Check contract/discovery.** Update stale static documentation only. Compare controller route/method/policy/actor/request/response metadata and actual JSON fields against P02. Assert internal operations/types are absent from API Explorer; the public response graph and public operation examples contain no correct-answer field. Anonymous `/health` stays `200 {status:ok}`, with no dependency details. Run the dependency-free contract checker.
6. **Independent Testing → Validate → Review.** Audit every acceptance scenario and run fresh complete gates. Production fixes return through Dispatcher; no skipped/deleted tests or weakened security. Final read-only review must have no unresolved high/critical findings. Dispatcher alone commits/pushes and opens the draft PR after passing evidence, links this plan/#41/#11/#7 and requests independent human review. Never claim human signoff or close the issue on code generation.

## Acceptance coverage

| Criterion | Steps and evidence |
| --- | --- |
| Public GET count10 →200, scope `questions.read`, count1–50, out-of-range400, empty200[] | Steps 3–4; actual public endpoint tests, defaults/boundaries and input tests. |
| Internal POST count5 →200 exact immutable snapshot, machine-only `questions.play`, shortage409 | Steps 3–4; actual internal endpoint tests with real service/repository double and typed shortage mapping; existing #38–40 invariants remain unchanged. |
| No public answer DTO/browser Swagger sample | Steps 3–5; exact public JSON fields, public contract example/schema closure and API Explorer exclusion of internal controller. |
| Exact route/status and safe domain-to-ProblemDetails mapping | Steps 2–5; HTTP400/401/403/409/503/500 content/shape/leak tests and metadata comparison. |
| Named policies, valid trusted actors, file ownership/layer boundaries | Steps 3–6; both real-bearer route matrices, spoofed input regression, architecture suite and final diff review. No caller ownership requirement exists for these routes beyond actor policy. |
| Real bearer success/error tests; public health; OpenAPI versus P02 | Steps 4–6; actual route bearer matrix, anonymous health and contract tests/checker. No fake auth. |
| Applicable backend/frontend demonstration | Full backend gates plus scoped HTTP/security tests. Frontend untouched; client/e2e gates do not apply to this backend-only change. |
| Actual plan, validation, read-only review and independent human gate | Step 6; tracked plan and final PR evidence; independent human approval remains pending, not invented. Dispatcher final-only gate supersedes historical intermediate-gate wording and plan-path convention. |

## Testing strategy

Use xUnit and FluentAssertions, descriptive `Method_Scenario_ExpectedBehavior` names and Arrange/Act/Assert. Extend existing test projects; no special dispatcher test project/folder. Each test host/double owns its mutable state; no test-order assumptions.

- **Public HTTP:** default10, bounds1/50, small/empty catalog, exactly `id,prompt,category,options`, four options, no `correctAnswer`, and real-service-to-repository delegation.
- **Internal HTTP:** valid count5 yields five distinct response items including answer from snapshot; preserve exact field/array shape, count forwarded once and snapshot independence. Insufficient catalog returns409.
- **Binding:** public0/51/negative/malformed counts →400; internal absent/empty/null/non-object/malformed JSON, missing/null/wrong/noninteger count and unknown members →400. Unauthenticated/forbidden requests with invalid bodies fail authorization first and do not call the application service.
- **Both actual routes:** missing/malformed tokens, bad signature, unknown key, expired token, wrong issuer/audience and ID-token misuse →401; missing/wrong/substr scope, wrong actor and wrong client →403; required exact scope and supported `scp` claims succeed. Browser tokens with `questions.play` never obtain answer material. Assert Bearer challenge on401.
- **Trusted boundary:** actor issuer/subject comes from valid token, ignoring spoofed query/header identity. Cancellation received by the application service is the request-aborted token; client-aborted operation propagates cancellation without false successful/failure payload.
- **Errors:** safe400/401/403/409/503/500 bodies contain only approved Problem fields; sentinel answer/request/SQL/exception text never appears. Test recognized timeout dependency, unexpected exception and typed invalid-count/shortage paths. Avoid artificial SqlException construction where timeout + wrapped-cause coverage is sufficient; inspect explicit SQL classifier in review.
- **Contract/health:** committed operations match route verbs, named policies, actor kinds, input/count bounds, response arrays and documented errors; public examples/response types remain answer-safe; API Explorer omits internal action. Health is anonymous200, including with an irrelevant invalid token, and carries only liveness data.
- Existing unit, persistence and security suites continue passing. No changed business rule requires separate new Domain unit coverage; all new endpoint/error behavior is exercised through HTTP. Testing worker may add meaningful missing coverage within these existing projects.

## Validation commands

Run from repository root using the configured .NET10 SDK and disposable SQL Server LocalDB integration databases. Use serial MSBuild to avoid this host's parallel restore/compiler stalls; test/format may require normal tool escalation for named-pipe/process restrictions. Preserve actual exit codes and totals; historical counts are not acceptance targets.

```powershell
dotnet restore QuizArena.sln --disable-parallel -m:1
dotnet build QuizArena.sln --no-restore -warnaserror -m:1
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test tests/QuizArena.QuestionBank.UnitTests/QuizArena.QuestionBank.UnitTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.QuestionBank.IntegrationTests/QuizArena.QuestionBank.IntegrationTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj --no-build --verbosity normal
dotnet test QuizArena.sln --no-build -m:1 --verbosity normal
dotnet test QuizArena.sln --no-build -m:1 --collect:"XPlat Code Coverage"
node contracts/validate-contracts.mjs
git diff --check
```

Offline restore may use the existing `C:\Users\kilda\.nuget\packages` cache as an explicit source if the normal network source is unavailable; record this exact environmental adaptation. Run no frontend commands unless frontend files become necessary (which requires a scoped plan adjustment). No external-provider E2E is introduced: security tests use ephemeral signed keys/local metadata with the real bearer handler, and no authentication test is silently skipped.

## Risks and exclusions

- Model-binding error details can echo untrusted input; the safe MVC response factory is mandatory and needs real HTTP malformed-input regression coverage. Preserve the #40 ordinary request class to avoid positional-record validation metadata failures.
- The static full contract deliberately contains internal answer-bearing snapshots. It must stay backend-only, while runtime API discovery omits that operation. Do not add a browser Swagger endpoint or package just to expose the internal contract.
- Unknown exceptions must stay500; only known count/catalog/dependency cases receive mapped statuses. Correct answers never enter ProblemDetails.
- Existing #39 repository/domain invariants own valid selection and snapshot independence; no new persistence/schema/seed work, no peer HTTP adapter, no new operation and no frontend expansion.
- No new runtime credentials, packages, project references or shared policy changes. No instruction weakening, Git/GitHub publication by workers, merge or manual issue closure.
- No functional blockers identified. Project-status access is unavailable; record the limitation in final handoff. Human approval remains pending on the draft PR.
