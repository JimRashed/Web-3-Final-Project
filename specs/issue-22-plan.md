# Issue #22: shared React frontend foundation

## Summary and architecture

Issue [#22](https://github.com/JimRashed/Web-3-Final-Project/issues/22), parent [#8](https://github.com/JimRashed/Web-3-Final-Project/issues/8), epic [#7](https://github.com/JimRashed/Web-3-Final-Project/issues/7). Base: `feature/milestone-3-layered-service-development` at `93c9f81`; branch: `feature/m3/22-react-frontend`.

Create a React and TypeScript browser shell with navigation, an OIDC session, six independently configured REST clients, and reusable request and field states. Node/npm provides build and test tooling. The six .NET services own all business rules and persistence. Browser code consumes public contracts only; it does not receive provider passwords or machine credentials.

Use the maintained `keycloak-js` adapter for authorization code flow with PKCE S256. Follow the [official JavaScript adapter documentation](https://www.keycloak.org/securing-apps/javascript-adapter). The configured public client is `quizarena-client`, authority is `http://localhost:8080/realms/quizarena`, callback is `/auth/callback`, and post-logout return is `/` on `http://localhost:5173`. Access and refresh tokens stay in adapter memory. Temporary protocol callback metadata is adapter-managed and must not contain application response data or persisted tokens.

Existing `frontend` CORS middleware already permits `http://localhost:5173` on service ports 5101–5106. This issue requires no backend production or contract changes. Authentication and authorization remain enforced by the APIs; UI guards only control presentation.

This plan is agent-reviewed through Dispatcher. The active repository workflow supersedes historical intermediate human gates. Independent human signoff remains pending on the final draft PR.

## File inventory

Create these files under `src/QuizArena.Client/`:

| File | Purpose |
|---|---|
| `package.json`, `package-lock.json` | Pinned React, React Router, Keycloak, Vite, TypeScript, ESLint, Vitest and Testing Library dependencies; reproducible scripts |
| `index.html`, `tsconfig.json`, `vite.config.ts`, `eslint.config.js`, `.env.example` | Entrypoint, compiler/bundler/test/lint setup and public configuration examples |
| `src/vite-env.d.ts`, `src/main.tsx` | Vite environment types and single session initialization before rendering |
| `src/config.ts`, `src/config.test.ts` | Validate public OIDC configuration and independent service base URLs |
| `src/app/App.tsx`, `src/app/App.css`, `src/app/routes.ts`, `src/app/App.test.tsx` | Responsive accessible shell, navigation, route placeholders, home, callback and missing-route presentation |
| `src/auth/session.ts`, `src/auth/session.test.ts` | Adapter boundary, memory session snapshot, bounded refresh, login/logout, cancellation and lifecycle tests |
| `src/auth/AuthProvider.tsx`, `src/auth/AuthProvider.test.tsx` | React session subscription and sign-in/expiry/error UI integration |
| `src/api/httpClient.ts`, `src/api/httpClient.test.ts`, `src/api/clients.ts`, `src/api/clients.test.ts` | Shared fetch/error/cancellation behavior and six configured clients |
| `src/components/RequestState.tsx`, `src/components/RequestState.test.tsx` | Loading, empty, error, retry, sign-in, permission and missing-resource states |
| `src/components/ValidatedField.tsx`, `src/components/ValidatedField.test.tsx`, `src/components/validation.ts`, `src/components/validation.test.ts` | Accessible label/help/error association and reusable pure field validators |
| `src/test/setup.ts` | Testing Library cleanup and DOM matchers |
| `src/test/doubles.ts` | Shared deterministic adapter/session/deferred helpers for meaningful lifecycle and request tests |

Other changes:

| File | Change |
|---|---|
| `.gitignore` | Ignore frontend dependencies, build/coverage output, local environment and test artifacts |
| `docs/milestone3/frontend-setup.md` | Setup, environment variables, routes, auth/client integration examples, validation and live smoke instructions |
| `specs/issue-22-plan.md` | This scoped plan and concise completed validation references |

No files are deleted. Preserve historical `specs/implementation-plan.md`. Implementation may adjust a filename when cohesion or the selected library requires it; record any necessary inventory adjustment here before handoff. Do not introduce a dispatcher-specific test folder.

## Ordered implementation steps

1. **Establish the baseline and toolchain.** Run the existing .NET build and relevant tests before edits, recording any environmental failure separately. Create the client package, committed lockfile, TypeScript/Vite/ESLint/Vitest setup, entrypoint and ignore rules. Define `dev`, `typecheck`, `lint`, `test`, and `build`; use strict port 5173 to match the provider's exact origin. Pin the installed versions and document a supported Node version. Completion: dependency installation, typecheck and a minimal production bundle run successfully.

2. **Validate public configuration.** `config.ts` reads public `VITE_` settings for provider URL/realm/client ID and independent Identity, Statistics, QuestionBank, Lobby, Gameplay and Notification bases, defaulting to the established local setup. Reject malformed/non-HTTP(S) URLs, embedded credentials and ambiguous query/fragment bases; allow local HTTP for development and document HTTPS deployment. Keep application secrets out of all browser settings. Completion: tests verify defaults, six independent overrides and invalid settings.

3. **Implement the session lifecycle.** Initialize one Keycloak adapter instance once, outside React mount effects. Explicitly use standard flow, `pkceMethod: 'S256'`, fixed callback and disabled login iframe where appropriate for browser privacy. Request only `openid profile` and the twelve established user scopes: `profiles.read profiles.write stats.read leaderboard.read questions.read lobbies.read lobbies.write matches.read matches.write events.read preferences.read preferences.write`. Do not auto-trigger login on every render or after refresh failure. Expose a small injected adapter interface for deterministic tests, subscription snapshot, explicit login/logout, and current access-token acquisition. Refresh once before a request when required; share an in-flight refresh across concurrent calls. On refresh failure or invalid session, clear tokens and user state, notify subscribers, abort active requests, and expose an explicit sign-in action. Logout clears local state before navigating to provider logout and still clears locally if provider logout fails. Guard asynchronous completions so a late callback or refresh cannot restore state after logout. Adapter initialization handles the callback before route rendering and removes protocol parameters using supported adapter behavior. Completion: tests cover explicit redirect parameters, callback/initialization failure, successful refresh, concurrent refresh, refresh failure, logout and late completion; tokens never enter application storage or logs.

4. **Build six REST clients.** `clients.ts` creates named clients for all six configured origins. `httpClient.ts` obtains the current access token immediately before authenticated fetch, adds `Authorization: Bearer`, uses the configured origin only and omits cookies. Reject absolute/protocol-relative/external paths, backslash ambiguities and internal routes before obtaining/sending a token. Propagate caller `AbortSignal`, cancel on session teardown, and remove cancellation listeners/controllers on completion. Handle JSON and empty success responses; parse safe ProblemDetails/validation fields without rendering raw HTML or unsafe response bodies. Represent 400, 401, 403, 404, 409, 500, 503 and network failure distinctly. A 401 ends the session and offers sign-in, a 403 offers a permission message, and no failure automatically loops or retries a mutation. A retry requires explicit caller/user action. Completion: tests assert each origin, bearer attachment/current token, no ID/refresh-token use, URL confinement, abort propagation/cleanup, session teardown, response parsing and bounded failure behavior.

5. **Create the shell and shared states.** Add home and eight navigable destinations: `/profile`, `/questions`, `/lobby`, `/match`, `/statistics`, `/leaderboard`, `/events`, `/preferences`, plus `/auth/callback` and not-found handling. Keep each destination a deliberate shell placeholder; the six feature tasks own data screens. Navigation identifies the active route, supports keyboard use and works on narrow screens. Provide visible sign-in/sign-out/session-expiry status. Reusable `RequestState` handles loading/empty/error/retry/401/403 and missing-resource presentations with appropriate accessible roles. `ValidatedField` associates label, help and errors using stable IDs; pure validators cover required/trimmed text and caller-supplied length/rule constraints without inventing business behavior. Completion: component tests cover all destinations, auth-dependent presentation, retry/sign-in callbacks, field validation and accessibility associations.

6. **Document and verify the integration.** Write setup and extension examples for feature owners. Document public environment settings, six service launch commands/ports, exact provider callback/origin settings, memory-token behavior, explicit login after reload when appropriate, refresh failure, and error/cancellation usage. Run the required frontend and .NET gates. Existing six `SecurityMatrixTests` provide actual configured-origin preflight allow/reject evidence. Where a browser and running APIs are available, verify positive cross-origin `/health` fetches from the frontend origin; do not infer CORS from configuration text alone. Record whether live Keycloak login, callback, refresh and logout were exercised. Completion: actual commands/results and truthful remaining limitations are available to read-only Code Review and the draft PR.

## Acceptance coverage

| Issue criterion | Steps and evidence |
|---|---|
| React/TypeScript, Node/npm, routing/navigation for eight destinations | 1, 5; lockfile, route tests, typecheck, lint, build |
| PKCE login/logout/refresh with memory-only tokens and no browser secret | 2, 3; explicit adapter options, lifecycle tests, storage/diff inspection |
| Independently configured bearer clients for all six APIs | 2, 4; configuration/origin/header tests |
| Distinct service ports and CORS | 4, 6; six real middleware preflight tests, positive browser smoke when services run |
| Loading/empty/error/retry/401/403 and field validation | 5; accessible component/validator tests |
| Abortable requests | 3, 4; caller-abort and session-teardown tests |
| Logout clears client state; refresh failure returns to sign-in without loops | 3–5; lifecycle, concurrency and UI tests |
| REST/public DTOs, .NET persistence, correct ownership/layer/scope boundaries | 2–6; client-only changes, no internal/machine integration, final diff review |
| Actual plan, validation and read-only review evidence | This plan, 6 and dispatcher reports; link in PR |
| Independent human approval and no premature issue closure | Final draft PR, `Human signoff: pending`; Dispatcher never merges/closes manually |

## Testing strategy

Use Vitest and Testing Library for this new frontend. Inject the Keycloak adapter and fetch at the auth/HTTP boundaries; tests establish application behavior, not a claim that a real provider authenticated. Include successful and failed adapter initialization, exact login configuration, refresh success/failure/concurrency, explicit logout including provider failure, stale asynchronous completion after logout, bearer attachment, URL confinement, 401/403 separation, cancellation and accessible state rendering. Avoid storing real credentials or tokens in fixtures. A harmless dummy bearer string is sufficient for header assertions.

Run existing xUnit/FluentAssertions/Moq backend suites and all six service integration projects as repository gates, including real CORS middleware tests. This issue changes no C# rule or endpoint, so it does not add synthetic backend tests. Existing relational suites require SQL Server/LocalDB and must not be skipped, deleted or replaced to obtain green checks.

A broad browser E2E suite for later feature screens is outside P07. If a small deterministic browser smoke is added to satisfy a concrete shell risk, list its exact files here and label simulated provider data. Live OIDC verification remains a separate provider-dependent check. The milestone-wide `test:e2e` work is owned by the later CI/E2E tasks; do not invent a successful result for an absent script.

## Validation commands and evidence

From repository root:

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal
dotnet test QuizArena.sln --no-build --collect:"XPlat Code Coverage"
git diff --check
```

From `src/QuizArena.Client`:

```powershell
npm ci
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

Record exact exits, totals, warnings and failures from this run. Record live smoke prerequisites/results separately: six APIs on 5101–5106, client on `http://localhost:5173`, configured Keycloak on 8080 and user-supplied demo credentials. Use the existing provider setup documentation; never print or commit credentials. No live provider or APIs were listening during Prime, and Docker was absent from PATH. This is an environment risk, not proof of provider behavior.

## Risks, constraints and exclusions

- Access and refresh tokens must remain in memory; browser callback state managed by the adapter is distinct from application token persistence. Check the installed adapter's behavior and use its supported APIs.
- Rotating refresh tokens require one shared refresh operation; teardown must win over late asynchronous completions.
- Independent base URLs are trusted deployment configuration, but per-request paths must not redirect bearer tokens to another origin. Keep fetch redirects from leaking credentials and avoid logging errors that include tokens or unsafe bodies.
- Provider and SQL Server availability may block a required check. Report the exact limitation; do not claim all gates passed or fabricate historical counts.
- Shell placeholders do not implement profile, questions, lobby, match, statistics, leaderboard, events or preferences features. No API/business/domain/data changes, migrations, Express server, MongoDB, direct SQL, machine scope, password form, token minting, client secret, generated internal client, CI overhaul or unrelated refactor.
- Final publication belongs to Dispatcher after successful current validation and read-only review. Draft PR targets the milestone branch, includes `Closes #22`, parent #8, epic #7, this plan and actual evidence. Independent human review remains pending; no automatic merge or manual closure.

## Implementation and repair notes

- Testing found that raw tab, LF and CR characters could be removed by URL normalization after the initial route guard. Repair cycle 1 rejects raw controls before parsing/token access, rejects decoded controls and validates the final canonical decoded pathname, including configured prefixes. Regression coverage preserves the original failing cases and adds encoded controls and a canonical-prefix case.
- Dependencies use the patched pinned toolchain and committed lockfile. Current npm audit: eleven moderate transitive `minimatch` developer-tool findings; zero high/critical. No force or legacy-peer resolution workaround was used. Fresh validation after the repair is required before publication.
