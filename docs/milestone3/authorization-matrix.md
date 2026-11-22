# QuizArena authorization matrix

Shipped routes verified against code at `c8dcc78` (#137). Picture rows proposed; team approval pending. See [security decisions](security-decisions.md).

All paths below are relative to the owning service origin. Audience is `quizarena-<service>`. Token validation (below) runs before any scope or ownership check. Differences between the shipped code and the target rules are listed as `DR-n` in the [discrepancy register](security-decisions.md#discrepancy-register); this document records both instead of silently rewriting the target.

Keycloak public client `quizarena-client` receives only user scopes via authorization-code PKCE S256. Confidential client `quizarena-gameplay` receives only `lobbies.verify questions.play results.write events.write` and the four corresponding audiences via client credentials. Machine credentials stay server-side. Obtain destination tokens scoped to that resource; do not forward browser tokens to internal routes. The browser requests all 12 user scopes in one token, which therefore carries six audiences; each host still checks its own audience and exact scope (SEC-12, DR-6).

## Token validation (all six hosts)

`src/QuizArena.<Service>.Api/Security/SecurityExtensions.cs` is identical on all six hosts apart from its namespace:

- **Issuer** must equal the configured `Authentication:Authority` with any trailing `/` trimmed. One issuer per deployment (local: `http://localhost:8080/realms/quizarena`).
- **Audience** must equal the host's `Authentication:Audience` (`quizarena-<service>`).
- **Signature** is required; RS256 only, keys from the issuer's metadata.
- **Lifetime**: `exp` is required and validated with 30 s clock skew. Realm access tokens live 300 s.
- **Token type**: the `typ` claim must be `Bearer`, so ID and refresh tokens are rejected.
- **401** returns problem+json "Missing or invalid bearer token" with `WWW-Authenticate: Bearer` (no token) or `Bearer error="invalid_token"` (rejected token).
- **403** returns problem+json "Insufficient scope, actor kind or ownership" (Lobby rewrites the title, DR-4).
- **Startup fails fast** when `Authority` (absolute URI), `Audience`, `BrowserClientId` or `MachineClientId` is missing.
- **Fallback policy** requires an authenticated user, so an endpoint without an explicit policy still needs a valid token. Only `/health`, the retired routes and the development-only `/openapi/v1.json` (Identity, Statistics) allow anonymous access.
- **CORS** allows only the configured origins, methods GET, POST, PUT and DELETE, and request headers `Authorization` and `Content-Type`. No response headers are exposed.

## Actor policies (as shipped)

Each named policy equals its scope (`<Service>Policies.cs`) and is evaluated by `ScopeAuthorizationHandler.cs`:

- **User policy**: `azp` equals `quizarena-client` AND `qa_actor` equals `user` AND a non-blank `sub` AND the exact scope.
- **Machine policy**: `azp` equals `quizarena-gameplay` AND `qa_actor` equals `machine` AND a non-blank `sub` AND the exact scope.

There is no configured service-account subject allowlist in the shipped code; the previous target ("a configured service-account subject belonging to that client") is tracked as DR-1.

Both policies, as shipped:
- Actor kind is never inferred from subject text.
- Extra scopes never turn machine credentials into user credentials.
- Scope matching is whitespace-delimited exact membership over `scope`/`scp`, never substring matching.

Target rules kept from the previous matrix:

- **Machine policy (target):** also require a configured service-account subject belonging to `quizarena-gameplay` (DR-1).
- **User policy (target):** reject service-account identities. Currently moot: `quizarena-client` is a public client with `serviceAccountsEnabled: false` in `infra/oidc/quizarena-realm.json`, so no service-account token can carry `azp=quizarena-client`. Re-evaluate if that client ever gains a service account.

## Route table

Issuer for every protected row: the configured Authority (above). "auth" in the responses column means token validation (401) and then the actor policy (403) from the sections above. Responses are listed in the order the code checks them. "DB outage" is the status a database failure produces today.

| Service | Method / route | Actor | Audience | Exact scope | Ownership / resource condition | Shipped responses (check order) | Notes |
|---|---|---|---|---|---|---|---|
| identity | GET `/api/users/me` | user | quizarena-identity | profiles.read | validated issuer + subject | auth → 404 no profile → 200 | DB outage 500, contract says 503 (DR-3) |
| identity | PUT `/api/users/me` | user | quizarena-identity | profiles.write | validated issuer + subject | auth → 415 non-JSON Content-Type (`[Consumes("application/json")]`) → 400 invalid body or display name → 200 (create or rename) | 415 is not in identity.yaml (DR-9); DB outage 500 (DR-3) |
| identity | GET `/api/users/me/picture` | user | quizarena-identity | profiles.read | validated issuer + subject; own picture only | auth → 404 no profile or picture → 200 image bytes; 503 storage unavailable | **Proposed (#144).** `Cache-Control: private, no-store`, `nosniff` (SEC-08) |
| identity | PUT `/api/users/me/picture` | user | quizarena-identity | profiles.write | validated issuer + subject; one picture per profile | auth → 415 type → 413 over 2,097,152 bytes → 400 empty, malformed header or bytes ≠ Content-Type → 404 no profile → 409 lost a concurrent replacement (first committer wins; sequential requests each replace) → 200 Profile; 503 storage unavailable | **Proposed (#144).** PNG/JPEG/WebP only (SEC-02, SEC-06, SEC-07) |
| identity | POST `/api/auth/register` | public | — | — | none | 410 always; body and bearer header ignored | Retired |
| identity | POST `/api/auth/login` | public | — | — | none | 410 always; body and bearer header ignored | Retired |
| identity | GET `/api/users/{id}` | public | — | — | none | 410 always | Retired |
| identity | GET `/health` | public | — | — | none | 200 `{status: ok}` | Liveness only |
| statistics | POST `/api/results` | machine | quizarena-statistics | results.write | none; results keyed by the machine token's issuer + matchId + userId | auth → 400 invalid → 409 changed replay → 200 empty (new or identical replay) | DB outage 500 (DR-8) |
| statistics | GET `/api/players/{userId}/stats` | user | quizarena-statistics | stats.read | path userId equals validated subject; data keyed by issuer + subject | auth → 403 not self (before lookup) → 200 | DB outage 500 (DR-8) |
| statistics | GET `/api/players/{userId}/matches` | user | quizarena-statistics | stats.read | path userId equals validated subject | auth → 400 paging → 403 not self → 200 | DB outage 500 (DR-8) |
| statistics | GET `/api/leaderboard` | user | quizarena-statistics | leaderboard.read | none; scoped to the caller's issuer | auth → 400 top → 200 | DB outage 500 (DR-8) |
| statistics | GET `/health` | public | — | — | none | 200 | |
| questionbank | GET `/api/questions/generate` | user | quizarena-questionbank | questions.read | none | auth → 400 count outside 1..50 → 200; 503 DB outage | Never returns correctAnswer |
| questionbank | POST `/internal/question-sets` | machine | quizarena-questionbank | questions.play | none | auth → 400 → 409 insufficient catalog → 200 five snapshots; 503 DB outage | Hidden from the generated OpenAPI document |
| questionbank | GET `/health` | public | — | — | none | 200 | |
| lobby | POST `/api/lobbies` | user | quizarena-lobby | lobbies.write | none; caller becomes host | auth → 400 body not `{}` → 201 + Location; 503 DB outage | Subject-only keys (DR-5); 403 title (DR-4) |
| lobby | GET `/api/lobbies/{joinCode}` | user | quizarena-lobby | lobbies.read | none; response booleans relative to caller | auth → 400 join code → 404 → 200 | DR-4, DR-5 |
| lobby | POST `/api/lobbies/{joinCode}/join` | user | quizarena-lobby | lobbies.write | none | auth → 400 → 404 → 409 full or locked → 200 (idempotent for a member while the lobby is open; a member rejoining a locked lobby gets 409) | DR-4, DR-5 |
| lobby | POST `/api/lobbies/{joinCode}/lock` | user | quizarena-lobby | lobbies.write | caller is lobby host | auth → 400 → 404 → 403 not host → 409 not two members → 200 (repeat lock 200) | DR-4, DR-5 |
| lobby | GET `/internal/lobbies/{joinCode}` | machine | quizarena-lobby | lobbies.verify | none | auth → 400 → 404 → 200 | Exposes host and player subjects to the machine client only |
| lobby | GET `/health` | public | — | — | none | 200 | |
| gameplay | POST `/matches` | user | quizarena-gameplay | matches.write | verified locked lobby host | auth → 400 joinCode → 404 lobby → existing match: 403 not host, else 200 → 409 unlocked → 403 not host → 409 membership → 201 + Location; 503 Lobby/QuestionBank unavailable | **409 before 403 for an unlocked lobby (DR-2)**; subject-only keys (DR-5); 403 title (DR-4) |
| gameplay | GET `/matches/{matchId}` | user | quizarena-gameplay | matches.read | match participant | auth → 404 → 403 outsider → 200 | DR-4, DR-5; DB outage 500 (DR-8) |
| gameplay | GET `/matches/{matchId}/next-question` | user | quizarena-gameplay | matches.read | match participant | auth → 404 → 403 outsider → 409 finished → 200 | Never returns correctAnswer; DR-4, DR-5 |
| gameplay | POST `/matches/{matchId}/answers` | user | quizarena-gameplay | matches.write | match participant | auth → 400 body → 404 → 403 outsider → 409 finished, stale or duplicate → 400 unknown option → 200 | DR-4, DR-5 |
| gameplay | GET `/health` | public | — | — | none | 200 | |
| notification | POST `/events` | machine | quizarena-notification | events.write | none; events keyed by the machine token's issuer + recipient | auth → 400 → 409 changed replay → 202 (identical replay returns the stored event) | DB outage 500 (DR-8) |
| notification | GET `/events` | user | quizarena-notification | events.read | validated issuer + subject; optional legacy userId must equal caller | auth → 400 paging → 403 userId ≠ subject → 200 | 403 title "Forbidden" (DR-4) |
| notification | GET `/users/{userId}/preferences` | user | quizarena-notification | preferences.read | path userId equals validated subject | auth → 403 not self → 200 (false/false default, not persisted) | |
| notification | PUT `/users/{userId}/preferences` | user | quizarena-notification | preferences.write | path userId equals validated subject | auth → 400 body → 403 not self → 200 | |
| notification | GET `/health` | public | — | — | none | 200 | |

The 30 shipped rows match the operation inventory in `contracts/validate-contracts.mjs`; the 2 picture rows are proposed.

## Security cases and evaluation order

For **every user row**: valid matching audience+scope+actor and required ownership reaches the documented success; absent/expired/wrong issuer/wrong audience/invalid signature token → 401; valid user token missing exact scope → 403; machine token → 403. For **every machine row**: machine token from `quizarena-gameplay` with destination scope reaches success; browser token → 403 even with accidentally granted scope; other client → 403; invalid/missing token → 401. An authentication failure includes WWW-Authenticate: Bearer. Health and retired rows require no token; health → 200 and retired → 410 even if an irrelevant bearer header is supplied. Target, not shipped: also reject machine subjects outside a configured allowlist (DR-1).

For **each self-only row** (Statistics stats/history, Notification preferences, optional events userId): caller player-a requesting player-b → 403 before data lookup, including nonexistent player-b. GET /api/users/me, PUT /api/users/me and the proposed picture routes derive issuer/subject exclusively from claims. Reject extra userId/hostId/playerId properties in user mutation bodies → 400; never use them to select an actor.

For **each match read/answer row**: nonexistent match → 404; existing match with outsider → 403; participant follows state rules. This intentionally reveals existence to authenticated scoped outsiders, but no match details. For **lock**: nonexistent lobby → 404; existing non-host → 403 before state validation; host with invalid state → 409. For **create-match** the target is the same order; shipped code returns 409 for an unlocked lobby before 403 for a non-host (DR-2). Lobby preview is available to any scoped user and exposes member count and caller-relative booleans, not member subjects. Internal Lobby lookup is machine-only and may expose identities for verification.

Negative input examples: count=0 or 51 → 400; page=0/pageSize=101 → 400; top=101 → 400; joinCode=abc123 → 400; displayName whitespace → 400; malformed GUID questionId → 400. New result score 6 → 400; conflicting replay → 409. User token calling /internal/question-sets → 403; public Question/CurrentQuestion response containing correctAnswer fails contract validation. Duplicate/stale/finished match answer → 409; GET next-question after finish → 409. Preference GET with no row returns false/false without persisting. Proposed picture: SVG or GIF → 415; 2,097,153 bytes → 413; PNG bytes sent as `image/jpeg` → 400.

Error precedence: authenticate → authorize scope/actor → validate syntax (400) → check claim-based self ownership → lookup → resource ownership → business state. Public retired routes skip all body validation; public health ignores irrelevant query/body and reports only liveness. Backend authorization is mandatory regardless of frontend guards. Shipped deviations: DR-2 (create-match order) and DR-3/DR-8 (a database outage returns 500 instead of 503 on Identity, Statistics, Gameplay and Notification).

## Client handling

401 prompts sign-in; 403 shows a permission message without retry loops; 400 maps safe field/message details; 404 missing-resource UI; 409 refreshes state and explains conflict; 503 offers a bounded user retry; safe 500 shows traceId for support. Proposed for the picture (#145): 413 explains the 2 MiB limit and 415 lists PNG, JPEG and WebP, both without automatic retry; 409 reloads the profile and lets the player retry. Tokens stay in memory and never appear in URLs/logs/localStorage; the picture is fetched with a bearer header and shown through a revocable object URL (SEC-08). Browser must not request machine scopes or call internal APIs. No public answer key, password/hash, machine secret or issuer administration data is returned.
