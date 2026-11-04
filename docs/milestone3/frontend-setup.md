# Shared React frontend (P07)

Issue [#22](https://github.com/JimRashed/Web-3-Final-Project/issues/22), parent #8, epic #7. The [scoped plan](../../specs/issue-22-plan.md) covers the frontend foundation. Feature screens are implemented by their service frontend tasks.

## Run locally

Use Node 22.12 or later and npm. From `src/QuizArena.Client`:

```powershell
npm ci
npm run dev
```

Open `http://localhost:5173`. Vite uses that exact host and strict port because Keycloak redirects and API CORS origins are registered explicitly. The production host must serve `index.html` for application routes, including `/auth/callback`, while serving built assets normally.

The shell contains overview, profile, questions, lobby, match, statistics, leaderboard, events and preferences destinations. It provides sign-in, sign-out, session status and accessible common states. It does not fabricate service data or implement the feature screens.

## Public configuration

Defaults work with the existing local service/provider configuration. To override them, copy the client `.env.example` to `.env.local` and restart Vite. Every `VITE_` value is public and embedded in the browser bundle. Never put a secret, password, access token, refresh token, connection string or machine credential in these variables.

| Variable | Default |
|---|---|
| `VITE_OIDC_URL` | `http://localhost:8080` |
| `VITE_OIDC_REALM` | `quizarena` |
| `VITE_OIDC_CLIENT_ID` | `quizarena-client` |
| `VITE_IDENTITY_API_URL` | `http://localhost:5101` |
| `VITE_STATISTICS_API_URL` | `http://localhost:5102` |
| `VITE_QUESTIONBANK_API_URL` | `http://localhost:5103` |
| `VITE_LOBBY_API_URL` | `http://localhost:5104` |
| `VITE_GAMEPLAY_API_URL` | `http://localhost:5105` |
| `VITE_NOTIFICATION_API_URL` | `http://localhost:5106` |

URLs must use HTTP(S), contain no embedded credentials, query or fragment, and point to their intended service. An optional path prefix is supported. Use HTTPS for deployed environments and configure each API's exact frontend origin and Keycloak's exact callback/post-logout URI for that deployment.

## Provider and six APIs

Follow [authentication setup](authentication.md) to start/provision Keycloak. The public browser client uses authorization code flow with PKCE S256, callback `http://localhost:5173/auth/callback`, and post-logout return `http://localhost:5173/`. The official [Keycloak adapter](https://www.keycloak.org/securing-apps/javascript-adapter) owns protocol handling. Browser login requests only the twelve P05 user scopes plus `openid profile`; no machine scopes or browser client secret are used.

From repository root, launch each required API in its own terminal:

```powershell
dotnet run --project src/QuizArena.Identity.Api
dotnet run --project src/QuizArena.Statistics.Api
dotnet run --project src/QuizArena.QuestionBank.Api
dotnet run --project src/QuizArena.Lobby.Api
dotnet run --project src/QuizArena.Gameplay.Api
dotnet run --project src/QuizArena.Notification.Api
```

Their launch profiles use ports 5101 through 5106 in that order. Existing security middleware allows `http://localhost:5173`, Authorization/Content-Type headers and supported REST methods. The CORS policy does not permit arbitrary origins. Service business endpoints are supplied by later tasks; `/health` is available independently.

## Session and feature integration

`Session` initializes the adapter once before the router reads a completed callback. Access/refresh tokens and the displayed user remain in memory. The adapter may briefly persist PKCE callback metadata; application tokens and service response data are never written to browser storage. Reloading loses the local session and requires an explicit sign-in; an existing provider session can make that sign-in immediate.

The session checks token validity before every authenticated request and shares one refresh operation among concurrent requests. Refresh failure and HTTP 401 clear local state, abort requests and present sign-in. They never automatically redirect or retry. HTTP 403 preserves the session and displays a permission message. Sign-out clears local state before navigating to provider logout; failed navigation cannot retain the local session. Late asynchronous initialization/refresh cannot revive a signed-out session.

Feature components obtain `session` from `useSession()`, create clients from `createClients(readConfig(import.meta.env), session)`, and consume public DTO types from their approved contracts. Example inside a component effect:

```tsx
const controller = new AbortController();
void clients.identity.request<MyProfile>('/api/users/me', {
  signal: controller.signal,
}).then(setProfile).catch((error: unknown) => {
  if (!(error instanceof DOMException && error.name === 'AbortError')) setError(error);
});
return () => controller.abort();
```

Clear feature response state on session changes and unmount it when the user is signed out. Avoid durable response caches containing player data. `session.signal` is renewed after teardown; clients capture the current signal for each request.

Clients attach only the current access token, omit cookies, reject redirects, and accept a service-relative path beginning with one `/`. Absolute URLs, path traversal, raw/encoded control characters, backslash ambiguities and internal routes are rejected before token acquisition. The final canonical decoded pathname is also checked, including any configured prefix. Requests return typed JSON or `undefined` for empty success. They never retry automatically, including mutations. `ApiError` distinguishes validation, unauthorized, forbidden, missing, conflict, unavailable, network and unexpected failures. Raw provider errors, HTML and ProblemDetails detail/title are not displayed. Valid string-array validation fields are exposed for field presentation; React renders them as text.

Use `RequestState` for loading, empty, retry, sign-in and permission states, and `ValidatedField` for stable label/help/error associations. Compose `required`, `maxLength` and caller-specific validators according to the approved DTO contract. API authorization remains authoritative.

## Validation

From the client directory:

```powershell
npm ci
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

Vitest/Testing Library tests inject an adapter/fetch double to verify app behavior, including PKCE options, explicit redirects, refresh failure/concurrency, logout races, bearer attachment, six independent origins, abort propagation and common UI states. These tests do not claim a live provider authenticated. Existing backend `SecurityMatrixTests` exercise real CORS middleware from the registered and an unknown origin. Dispatcher records fresh npm/.NET validation and read-only review evidence in the PR.

For a live smoke after provider/APIs are running:

1. Open the client on port 5173. Sign in using a locally provisioned demo account without sharing its credentials or tokens. Confirm callback returns to the overview and player name appears.
2. From that page, check cross-origin `/health` responses from all six service ports; browser network inspection should show the exact allowed origin. Use a public authenticated endpoint when its feature task provides one to verify bearer calls.
3. Let the access token expire, then make an authenticated request and verify refresh succeeds. Stop/invalidate the provider session and verify a failed refresh ends the local session with one sign-in action and no retry loop.
4. Sign out and verify the player state disappears immediately and provider logout returns to `/`. Reload and verify no stored access/refresh token restores the app session.

No provider or APIs were listening during Prime and Docker was absent from PATH. Live provider/CORS smoke has not been claimed by implementation. The later milestone E2E/CI tasks own a broad `test:e2e` suite; P07 supplies component and client/session tests.

Dependency validation for this dispatch uses the patched pinned toolchain and committed lockfile. The current npm audit reports eleven moderate findings in transitive `minimatch` developer tooling, with zero high or critical findings. No forced dependency resolution or legacy peer dependency workaround was used. The PR records the actual validation results and this remaining toolchain limitation.
