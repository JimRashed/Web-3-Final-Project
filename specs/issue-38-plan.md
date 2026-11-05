# Issue #38: QuestionBank domain rules

## Summary and architecture

[Issue #38](https://github.com/JimRashed/Web-3-Final-Project/issues/38) is the first sequential child of [QuestionBank feature #11](https://github.com/JimRashed/Web-3-Final-Project/issues/11). The P03 scaffold (#18) is merged into the milestone branch. Define pure QuestionBank-owned catalog and selection behavior in `QuizArena.QuestionBank.Domain`; persistence, application DTOs, HTTP authorization, and React belong to later child issues. The approved behavior is recorded in `docs/milestone3/api-contracts.md` and `contracts/openapi/questionbank.yaml`.

The legacy `QuizArena.Domain.QuestionBank` entities remain temporarily because the monolith still compiles against them. They must cease runtime ownership during the coordinated migration; this task must not break the existing solution by deleting them prematurely.

## File inventory

| Path | Change |
| --- | --- |
| `src/QuizArena.QuestionBank.Domain/Entities/Question.cs` | Add a validated Question aggregate with a Guid identity, prompt, category, four options, and correct answer. Keep its state immutable to callers. |
| `src/QuizArena.QuestionBank.Domain/Entities/QuestionSnapshot.cs` | Add an immutable answer-bearing value for a match; copy option values so later catalog changes cannot alter it. |
| `src/QuizArena.QuestionBank.Domain/Services/QuestionSelection.cs` | Add pure count validation and distinct selection without replacement, with public up-to-count and internal exact-count semantics. |
| `src/QuizArena.QuestionBank.Domain/Exceptions/InvalidQuestionException.cs` | Add typed exception for invalid catalog content. |
| `src/QuizArena.QuestionBank.Domain/Exceptions/InvalidQuestionCountException.cs` | Add typed exception for counts outside 1..50. |
| `src/QuizArena.QuestionBank.Domain/Exceptions/InsufficientQuestionCatalogException.cs` | Add typed exception for exact-count shortage, to be mapped to HTTP 409 in #41. |
| `tests/QuizArena.QuestionBank.UnitTests/Domain/QuestionTests.cs` | Test valid construction and each identity, text, length, option and correct-answer invariant. |
| `tests/QuizArena.QuestionBank.UnitTests/Domain/QuestionSelectionTests.cs` | Test bounds, empty/short catalog, no duplicate IDs, valid-only selection, exact snapshot count, and snapshot independence. |

`Category` is a nonblank string in the approved public contract. Do not introduce a separate category aggregate unless existing persistence requirements in #39 demand it. No project or package references need changing.

## Ordered steps

1. Add Question construction that rejects `Guid.Empty`; blank or overlength prompt (1000), category (80), options/answer (300); anything other than four options; blank or duplicate options; and a correct answer that does not match exactly one option using ordinal comparison. Copy the incoming options into immutable storage.
2. Add immutable `QuestionSnapshot` construction from a validated Question, retaining its Guid, prompt, category, ordered options and exact answer value. Neither caller-owned arrays nor later catalog object changes may mutate the snapshot.
3. Add pure QuestionSelection methods: validate count 1..50; select up to count distinct valid questions for public use (empty catalog yields empty result); select exactly count distinct questions for internal snapshots (shortage throws typed exception). Specify deterministic selection input or injected randomness so tests do not depend on ordering; do not use a database or HTTP types.
4. Add focused domain tests for all boundaries, ordinal matching, duplicate removal, shortage and immutable copies. Keep tests inside the existing service unit test project.
5. Run focused tests, solution gates and architecture checks. Inspect the diff for accidental legacy behavior changes or outer-layer references.

## Acceptance coverage

| Issue criterion | Steps/evidence |
| --- | --- |
| Guid ID; nonblank prompt/category; exactly four distinct nonblank options; one exact matching correct answer | Steps 1 and 4, `QuestionTests` |
| Count 1..50, distinct selection and empty catalog behavior | Steps 3 and 4, `QuestionSelectionTests` |
| Internal immutable snapshots prevent catalog changes affecting a match | Steps 2–4, snapshot tests |
| Domain/Data/Api ownership and no outer dependency | Domain-only files; inspect `.csproj` and architecture tests in step 5 |
| Focused validation and review evidence | Step 5; Dispatcher records actual results and a draft PR for #38 linked to parent #11; independent human signoff remains pending |

## Testing and validation

Use xUnit and FluentAssertions with `MethodName_Scenario_ExpectedBehavior` test names. Pure rules do not need mocks or a database. From the repository root, run:

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet test tests/QuizArena.QuestionBank.UnitTests/QuizArena.QuestionBank.UnitTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj --no-build --verbosity normal
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal
```

Record exact exits and test counts. SQL Server LocalDB may be required by existing integration tests; a missing runtime is an environmental blocker, not a passing test.

## Risks and exclusions

- The legacy monolith has mutable Question/Category types. Avoid deleting or redirecting them until consumers move in later tasks.
- The public answer-free DTO, internal HTTP response, EF mapping, and catalog filtering at persistence boundaries belong to #39–41. The domain must keep the answer private from public projections when those are added.
- #39 is blocked by open prerequisite #19. #41–43 also depend on open #21, #22 and #24. Do not claim the whole feature or child tasks accepted before those prerequisites and human review.
- This plan is agent-reviewed for automatic Dispatcher handoff, not human approval.
