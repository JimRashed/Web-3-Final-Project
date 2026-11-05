# Issue #42: QuestionBank React screen and REST integration

## Summary and architecture

Issue: [#42 — S03-T05](https://github.com/JimRashed/Web-3-Final-Project/issues/42), parent [#11](https://github.com/JimRashed/Web-3-Final-Project/issues/11), epic [#7](https://github.com/JimRashed/Web-3-Final-Project/issues/7).

Prime verified #42 open and assigned to JimRashed; hard prerequisites #17/#22 and live endpoint dependency #41 are closed. Work started on `feature/m3/42-questionbank-react` from milestone base `5f45f3ccc0f5558115a3e24b1a163f973a5c2449`; before final validation, the isolated branch was advanced to milestone base `8cefc008fa5057794f4334b48a3f6d56f6f89927` and the Questions route was integrated with the new upstream screens. This plan is agent-reviewed; independent human signoff remains pending at the final draft PR. Current repository dispatcher policy supersedes historical intermediate gates and plan paths.

Replace the `/questions` placeholder with an authenticated, accessible public catalog preview. The screen allows an integer count from 1 through 50, initially 10, and an explicit generate/regenerate action. It shows only question prompt, category and four options. Initial empty guidance avoids an unsolicited network request, consistent with the existing Lobby page. There is no answering, scoring or correctness feedback on this preview screen.

The feature-local API module uses `createClients(...).questionbank`, the shared session lifetime and HTTP client. It calls only `GET /api/questions/generate?count=<integer>` at the configured public origin. API authentication and `questions.read` remain authoritative. Browser code receives no database access, machine credentials, subject overrides, internal snapshot type or internal operation. Existing QuestionBank security extensions already supply the configured frontend CORS policy; no backend change or proxy is needed.

Normative sources: `docs/milestone3/api-contracts.md`, the public Question schema in `contracts/openapi/questionbank.yaml`, existing `src/QuizArena.Client/src/api/lobby.ts`, `auth/session.ts`, common request/field components and Lobby lifecycle tests. Do not import the full backend-only OpenAPI document into the browser bundle.

## Exact file inventory

| Action | File | Purpose |
|---|---|---|
| Create | `src/QuizArena.Client/src/api/questionbank.ts` | Public DTO, count guard, response validation/projection and abortable generate request |
| Create | `src/QuizArena.Client/src/api/questionbank.test.ts` | Contract, transport, validation, projection and cancellation tests |
| Create | `src/QuizArena.Client/src/features/questionbank/QuestionBankPage.tsx` | Session-gated preview, count form, request and recovery lifecycle |
| Create | `src/QuizArena.Client/src/features/questionbank/QuestionBankPage.css` | Feature-scoped responsive styling using existing shell conventions |
| Create | `src/QuizArena.Client/src/features/questionbank/QuestionBankPage.test.tsx` | Form, previews, request states and safe status handling |
| Create | `src/QuizArena.Client/src/features/questionbank/QuestionBankPage.lifecycle.test.tsx` | Cancellation, stale replies, teardown and session replacement |
| Modify | `src/QuizArena.Client/src/app/App.tsx` | Mount the real page only for `/questions` |
| Modify | `src/QuizArena.Client/src/app/App.test.tsx` | Update placeholder-specific expectation and verify real route/navigation |
| Modify | `docs/milestone3/frontend-setup.md` | Describe implemented Questions usage and explicit live-verification status/checklist |
| Create | `specs/issue-42-plan.md` | This scoped plan and acceptance mapping |

No deletions, dependency upgrades, lockfile changes, new test project, auth changes, backend changes or shared HTTP changes are planned. If evidence requires another file, record the reason before implementation and review it within #42 scope. Operational reports/logs remain ignored under `.dispatcher/runs/42-20261104/`.

## Ordered implementation steps

1. **Establish a current baseline.** Restore/build the solution and install the committed frontend dependencies; run the current frontend suite before edits. Record actual results and existing environment/toolchain limitations separately. Do not treat historical test counts as current evidence.
2. **Add the public API module.** Define only `id`, `prompt`, `category` and `options`. Guard finite integer counts 1–50 before fetching. Request through the existing QuestionBank client with an AbortSignal and no body/identity override. Validate unknown response arrays and every public field against the committed schema: UUID id; nonblank prompt/category/options within Unicode code-point limits 1000/80/300; exactly four unique string options. Permit an empty response and fewer than requested questions. Project a fresh object and option array so unknown fields, including answer material, never enter feature state. Do not invent stricter rules or require exact response count beyond the approved contract. Malformed success is a safe unexpected ApiError, not a rendered raw body.
3. **Build the session-gated page.** Preserve the Questions heading and navigation. Authenticated users see a labeled numeric count selector/input (default10, min1/max50/step1) and explicit Generate questions action; after a result offer Regenerate. Show initial guidance, empty catalog guidance, pending state and a Cancel action. Validate invalid/blank/fractional/out-of-range input locally, associate and focus the field error, and make no request. Prevent duplicate submissions synchronously while pending. Render category, prompt and options as escaped text in semantic lists/cards with stable keys; no correct-answer rendering or special correctness styling.
4. **Make request lifetime safe.** Abort on explicit cancel, unmount, sign-out/expiry and session replacement. Invalidate request generations so a transport that resolves after cancellation cannot restore stale previews/errors or clear a newer pending state. Clear feature data when the session ends or changes. Do not show cancellation as a service failure. If input remains editable during a request, keep the submitted count immutable for that request; changing it must never relabel stale results as a new request. Prefer disabling input while pending. Retain no durable cache or browser storage.
5. **Map failures and recovery.** Reuse safe ApiError/RequestState messages; never render upstream detail/title/trace/HTML or arbitrary field strings. Local count errors are field-level. A400 after valid local input gives safe validation guidance;401 ends the session and offers explicit sign-in;403 preserves it and explains permission;404/409/503/network/unexpected failures use safe user-triggered recovery as applicable. No automatic retries or redirects. Loading/anonymous/unavailable session states make no catalog requests. Sign-in stays explicit.
6. **Wire and document the feature.** Replace only the `/questions` Destination branch in App, retaining unrelated destinations. Adjust App tests that currently demand placeholder text for every destination. Document count bounds, public-only preview, cancellation and honest validation/live status. Include a real browser checklist instead of claiming fetch-double tests prove provider-backed acceptance.
7. **Testing, validation and read-only review.** Testing independently inspects/adds meaningful cases, then Validate runs fresh gates and coverage. Review receives the issue, plan, complete diff and actual evidence. Dispatcher alone publishes a conventional commit and draft PR after passing applicable code checks/review. Human approval and live acceptance must be reported accurately, never fabricated; do not close the issue on generation.

## Acceptance coverage

| Issue requirement | Steps and evidence |
|---|---|
| Count selector, question preview with options/category, regenerate | 2–3,6; API boundary tests, page tests and route test |
| Empty/loading/error/validation states | 3–5; initial/empty result, pending, malformed input and status matrix tests |
| Cancellation and submit state | 3–4; duplicate-submit, explicit cancel, teardown and stale-resolution tests |
| Never show correct answers or call internal question sets | 2–3; public field projection, injected extra-field sentinel DOM test and exact outgoing URL assertions |
| Committed contract, session/public DTOs only | 2,4; public schema boundary cases, configured origin/bearer tests, unchanged auth/database layers |
| Safe400/401/403/404/409/503 behavior as applicable | 5; real shared HTTP client with fetch responses and session assertions, no raw details/no retry loop |
| File ownership/layer/security policy | Inventory,2,6–7; complete diff inspection and read-only architecture/security review |
| Frontend typecheck/lint/components/build | 1,7; actual fresh command exits/counts |
| Real API screen exercise before closure | Live checklist below; actual environment probes/results, with incomplete acceptance explicitly pending when unavailable |
| Plan, validation, review, independent human approval | This file plus final draft PR evidence; automated stages are not human approval |

## Testing strategy

Use existing Vitest/Testing Library conventions and test locations; the changed behavior is TypeScript/React and does not warrant synthetic xUnit tests. API tests run the real shared HTTP/session clients with fetch doubles, verifying only the QuestionBank origin/public path, bearer attachment, count encoding, omission of cookies/body and propagated cancellation. Exercise count1/50 and invalid count0/51/NaN/infinity/fractions; valid Unicode/code-point boundaries; malformed response root/fields/options, four-option uniqueness, empty/short catalog and stripping unknown answer properties. Never treat these doubles as live backend evidence.

Component tests cover initial instructions, explicit sign-in, no requests for nonauthenticated/loading/unavailable sessions, form association/focus, successful count-specific generation/regeneration, escaped prompt/options/category, empty results, safe status matrix, retry after failure, busy duplicate guard and explicit cancel. Lifecycle tests deliberately resolve obsolete promises after cancel/unmount/session replacement, verify pending requests abort, and confirm neither old previews nor errors reappear. App tests verify route navigation reaches the real screen without changing other feature routes.

Existing backend regression/coverage is run fresh because dispatcher requires aggregate gates, despite no backend source changes. Existing endpoint security/CORS tests remain authoritative backend automated evidence; do not replace token validation with an acceptance bypass.

## Validation commands and environment

From repository root (.NET10 SDK, LocalDB for relational tests):

```powershell
dotnet restore QuizArena.sln --source C:\Users\kilda\.nuget\packages --disable-parallel -m:1 --verbosity minimal
dotnet build QuizArena.sln --no-restore -warnaserror -m:1 --verbosity minimal
dotnet format QuizArena.sln --verify-no-changes --no-restore --verbosity minimal
dotnet test QuizArena.sln --no-build -m:1 --verbosity normal --logger 'trx;LogFilePrefix=regression' --results-directory .dispatcher/runs/42-20261104/regression-results
dotnet test QuizArena.sln --no-build -m:1 --collect:'XPlat Code Coverage' --logger 'trx;LogFilePrefix=coverage' --results-directory .dispatcher/runs/42-20261104/coverage-results
git diff --check
```

The offline NuGet source uses the existing cache without changing dependencies; report a cache miss rather than weakening restore. From `src/QuizArena.Client` (Node>=22.12):

```powershell
npm ci
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

Record npm install audit output as observed; dependency upgrades are separate scope. `npm run test:e2e` is not currently defined: explicitly report this limitation rather than inventing a successful suite or introducing a broad milestone E2E framework. Required live evidence is distinct from component tests.

### Live browser checklist and evidence boundary

Probe the configured provider discovery endpoint and QuestionBank health without printing credentials; inspect whether a provisioned local catalog and normal user login are available. Docker absence alone does not prove a running provider absent. Start owned local development processes only when prerequisites are present, track their exact process ownership and stop only those processes afterward. Follow the existing frontend/authentication setup; never reset another user's database, weaken bearer validation, use a machine token for browsing or embed credentials/tokens in scripts/reports.

With normal provider login and real owned API/catalog, open `http://localhost:5173/questions`; verify count10 then1/50, public previews and regenerate, empty/loading/cancel as controllable, safe errors, session expiry/sign-out and no internal browser requests or answer fields. Record concrete observations and distinguish naturally unavailable fault scenarios from verified ones. If provider/catalog/browser capability is unavailable, report the exact live acceptance blocker and preserve the draft's pending live checklist; do not claim issue completion, invent human approval or silently substitute doubles for live evidence.

## Risks, exclusions and handoff

- Live provider/API/catalog and browser availability is not established by closed prerequisite issues. Its absence is an environment limitation, not a reason to alter frontend/backend security. Implementation can proceed with approved mock HTTP; live acceptance remains pending until actually demonstrated.
- GitHub Project status could not be read with current credential scope; report that rather than asserting Ready/In review.
- Shared session handling invalidates on401; feature recovery must not accidentally keep private state mounted or auto-login.403 must preserve the session.
- No backend DTO/service/persistence/security changes, internal snapshots, scoring, question authoring, filters/categories API, polling, machine credentials, new dependencies, shared auth refactors, broad E2E framework or unrelated route work.
- Dispatcher retains two additional repair cycles; worker stages do not commit, push, publish, merge or close issues. Final independent human PR review remains pending.
