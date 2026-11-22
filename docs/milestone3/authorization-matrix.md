# QuizArena authorization matrix

Route inventory established at `c8dcc78` (#137). SEC-B2 (#141) reconciles authorization for the shipped routes. Picture rows are proposed under #144. [Security decisions](security-decisions.md)

All paths below are relative to the owning service origin. Audience is `quizarena-<service>`. Token validation (below) runs before any scope or ownership check. Differences between the shipped code and the target rules are listed as `DR-n` in the [discrepancy register](security-decisions.md#discrepancy-register); this document records both instead of silently rewriting the target.

Keycloak public client `quizarena-client` receives only user scopes via authorization-code PKCE S256. Confidential client `quizarena-gameplay` receives only `lobbies.verify questions.play results.write events.write` and the four corresponding audiences via client credentials. Machine credentials stay server-side. Obtain destination tokens scoped to that resource; do not forward browser tokens to internal routes. The browser requests all 12 user scopes in one token, which therefore carries six audiences; each host still checks its own audience and exact scope (SEC-12, DR-6).

## Token validation (all six hosts)

`src/QuizArena.<Service>.Api/Security/SecurityExtensions.cs` is identical on all six hosts apart from its namespace:

- **Issuer** must equal the configured `Authentication:Authority` with any trailing `/` trimmed. One issuer per deployment (local: `http://localhost:8080/realms/quizarena`).
- **Audience** must include an exact match for the host's `Authentication:Audience` (`quizarena-<service>`). Valid tokens can carry additional audiences (SEC-12).
- **Signature** is required; RS256 only, keys from the issuer's metadata.
- **Lifetime**: `exp` is required and validated with 30 s clock skew. Realm access tokens live 300 s.
- **Token type**: the `typ` claim must be `Bearer`, so ID and refresh tokens are rejected.
- **401** returns problem+json "Missing or invalid bearer token" with `WWW-Authenticate: Bearer` (no token) or `Bearer error="invalid_token"` (rejected token).
- **403** returns problem+json with title "Insufficient scope, actor kind or ownership", a safe detail and a trace ID.
- **Startup fails fast** when `Authority` (absolute URI), `Audience`, `BrowserClientId` or `MachineClientId` is missing.
- **Fallback policy** requires an authenticated user, so an endpoint without an explicit policy still needs a valid token. Only `/health`, the retired routes and the development-only `/openapi/v1.json` (Identity, Statistics) allow anonymous access.
- **CORS** allows only the configured origins, methods GET, POST, PUT and DELETE, and request headers `Authorization` and `Content-Type`. No response headers are exposed.

## Actor policies (as shipped)

Each named policy equals its scope (`<Service>Policies.cs`) and is evaluated by `ScopeAuthorizationHandler.cs`:

- **User policy**: `azp` equals `quizarena-client` AND `qa_actor` equals `user` AND a non-blank `sub` AND the exact scope.
- **Machine policy**: `azp` equals `quizarena-gameplay` AND `qa_actor` equals `machine` AND a non-blank `sub` AND the exact scope.

The machine policy treats `sub` as an opaque identity issued by the trusted realm. It relies on Keycloak's service-account configuration and the signed client and actor claims (DR-1).

Both policies, as shipped:
- Actor kind is never inferred from subject text.
- Extra scopes never turn machine credentials into user credentials.
- Scope matching is whitespace-delimited exact membership over `scope`/`scp`, never substring matching.

Target rules kept from the previous matrix:

- **User policy (target):** reject service-account identities. Currently moot: `quizarena-client` is a public client with `serviceAccountsEnabled: false` in `infra/oidc/quizarena-realm.json`, so no service-account token can carry `azp=quizarena-client`. Re-evaluate if that client ever gains a service account.

## Route table

Issuer for every protected row: the configured Authority (above). "auth" in the responses column means token validation (401) and then the actor policy (403) from the sections above. Responses are listed in the order the code checks them. "DB outage" is the status a database failure produces today.

| Service | Method / route | Actor | Audience | Exact scope | Ownership / resource condition | Shipped responses (check order) | Notes |
|---|---|---|---|---|---|---|---|
| identity | GET `/api/users/me` | user | quizarena-identity | profiles.read | validated issuer + subject | auth → 404 no profile → 200 | DB outage 500, contract says 503 (DR-3) |
| identity | PUT `/api/users/me` | user | quizarena-identity | profiles.write | validated issuer + subject | auth → 415 unsupported Content-Type → 400 invalid body or display name → 200 (create or rename) | Accepts `application/json`; DB outage 500 (DR-3) |
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
| lobby | POST `/api/lobbies` | user | quizarena-lobby | lobbies.write | none; caller becomes host | auth → 400 body not `{}` → 201 + Location; 503 DB outage | Issuer-private subject keys (DR-5) |
| lobby | GET `/api/lobbies/{joinCode}` | user | quizarena-lobby | lobbies.read | none; response booleans relative to caller | auth → 400 join code → 404 → 200 | |
| lobby | POST `/api/lobbies/{joinCode}/join` | user | quizarena-lobby | lobbies.write | none | auth → 400 → 404 → 409 full or locked → 200 (idempotent for a member while the lobby is open; a member rejoining a locked lobby gets 409) | |
| lobby | POST `/api/lobbies/{joinCode}/lock` | user | quizarena-lobby | lobbies.write | caller is lobby host | auth → 400 → 404 → 403 not host → 409 not two members → 200 (repeat lock 200) | |
| lobby | GET `/internal/lobbies/{joinCode}` | machine | quizarena-lobby | lobbies.verify | none | auth → 400 → 404 → 200 | Exposes host and player subjects to the machine client only |
| lobby | GET `/health` | public | — | — | none | 200 | |
| gameplay | POST `/matches` | user | quizarena-gameplay | matches.write | verified locked lobby host | auth → 400 joinCode → 404 lobby → 403 not host → 200 existing match or 409 unlocked/invalid membership → 201 + Location; 503 Lobby/QuestionBank unavailable | Issuer-private subject keys (DR-5) |
| gameplay | GET `/matches/{matchId}` | user | quizarena-gameplay | matches.read | match participant | auth → 404 → 403 outsider → 200 | DB outage 500 (DR-8) |
| gameplay | GET `/matches/{matchId}/next-question` | user | quizarena-gameplay | matches.read | match participant | auth → 404 → 403 outsider → 409 finished → 200 | Never returns correctAnswer |
| gameplay | POST `/matches/{matchId}/answers` | user | quizarena-gameplay | matches.write | match participant | auth → 400 body → 404 → 403 outsider → 409 finished, stale or duplicate → 400 unknown option → 200 | |
| gameplay | GET `/health` | public | — | — | none | 200 | |
| notification | POST `/events` | machine | quizarena-notification | events.write | none; events keyed by the machine token's issuer + recipient | auth → 400 → 409 changed replay → 202 (identical replay returns the stored event) | DB outage 500 (DR-8) |
| notification | GET `/events` | user | quizarena-notification | events.read | validated issuer + subject; optional legacy userId must equal caller | auth → 400 paging → 403 userId ≠ subject → 200 | |
| notification | GET `/users/{userId}/preferences` | user | quizarena-notification | preferences.read | path userId equals validated subject | auth → 403 not self → 200 (false/false default, not persisted) | |
| notification | PUT `/users/{userId}/preferences` | user | quizarena-notification | preferences.write | path userId equals validated subject | auth → 400 body → 403 not self → 200 | |
| notification | GET `/health` | public | — | — | none | 200 | |

The 30 shipped rows match the operation inventory in `contracts/validate-contracts.mjs`; the 2 picture rows are proposed.

## Security cases and evaluation order

For **every user row**: valid matching audience+scope+actor and required ownership reaches the documented success; absent/expired/wrong issuer/wrong audience/invalid signature token → 401; valid user token missing exact scope → 403; machine token → 403. For **every machine row**: a validated token with the destination audience, exact scope, a non-blank subject, `azp=quizarena-gameplay` and `qa_actor=machine` reaches success; browser token → 403 even with accidentally granted scope; other client → 403; invalid/missing token → 401. An authentication failure includes WWW-Authenticate: Bearer. Health and retired rows require no token; health → 200 and retired → 410 even if an irrelevant bearer header is supplied.

For **each self-only row** (Statistics stats/history, Notification preferences, optional events userId): caller player-a requesting player-b → 403 before data lookup, including nonexistent player-b. GET /api/users/me, PUT /api/users/me and the proposed picture routes derive issuer/subject exclusively from claims. Reject extra userId/hostId/playerId properties in user mutation bodies → 400; never use them to select an actor.

For **each match read/answer row**: nonexistent match → 404; existing match with outsider → 403; participant follows state rules. This intentionally reveals existence to authenticated scoped outsiders, but no match details. For **lock** and **create-match**, an unknown lobby returns 404. An existing lobby requested by a non-host returns 403 before state validation. A host with an invalid lobby state receives 409. Repeated authorized starts return the existing match. Lobby preview is available to any scoped user and exposes member count and caller-relative booleans, not member subjects. Internal Lobby lookup is machine-only and may expose identities for verification.

Lobby and Gameplay enforce the same exact issuer before resolving a caller. Their stores are dedicated to that issuer and ownership uses opaque, ordinal subjects within those stores. Changing the configured issuer requires a new empty store or explicit identity migration. [ADR-05](architecture-decisions.md#owners-and-data-boundaries)

Negative input examples: count=0 or 51 → 400; page=0/pageSize=101 → 400; top=101 → 400; joinCode=abc123 → 400; displayName whitespace → 400; malformed GUID questionId → 400. New result score 6 → 400; conflicting replay → 409. User token calling /internal/question-sets → 403; public Question/CurrentQuestion response containing correctAnswer fails contract validation. Duplicate/stale/finished match answer → 409; GET next-question after finish → 409. Preference GET with no row returns false/false without persisting. Proposed picture: SVG or GIF → 415; 2,097,153 bytes → 413; PNG bytes sent as `image/jpeg` → 400.

Error precedence: authenticate → authorize scope/actor → validate syntax (400) → check claim-based self ownership → lookup → resource ownership → business state. Public retired routes skip all body validation; public health ignores irrelevant query/body and reports only liveness. Backend authorization is mandatory regardless of frontend guards. Database outages still return 500 on Identity, Statistics, Gameplay and Notification (DR-3/DR-8).

## SEC-B2 integration evidence

| Service | Test suites and coverage |
|---|---|
| Identity | `ProfileRouteSecurityTests` and `ProfileEndpointsTests` cover exact scope, actor kind, claim-derived profiles and unchanged profile storage after rejected inputs. |
| Statistics | `StatisticsRouteSecurityTests` and `StatisticsEndpointsTests` cover the machine-only result ingress, self-only history and stats, and unchanged result storage after denied writes. |
| QuestionBank | `QuestionRouteSecurityTests` and `QuestionSqlPrincipalIsolationTests` cover actor separation, private snapshots and unchanged catalog state. |
| Lobby | `LobbyRouteSecurityTests` and `LobbyPrincipalIsolationTests` cover the machine-only roster, canonical denials and unchanged roster and lock state after a denied mutation. |
| Gameplay | `GameplayRouteSecurityTests` and `GameplayApplicationTests` cover host-first creation, participant access and unchanged match, answer and delivery state after denied writes. |
| Notification | `NotificationRouteSecurityTests` and `NotificationEndpointsTests` cover machine-only event ingestion, self-only events and preferences, and unchanged event/preference storage after denied writes. |

## Client handling

401 prompts sign-in; 403 shows a permission message without retry loops; 400 maps safe field/message details; 404 missing-resource UI; 409 refreshes state and explains conflict; 503 offers a bounded user retry; safe 500 shows traceId for support. Proposed for the picture (#145): 413 explains the 2 MiB limit and 415 lists PNG, JPEG and WebP, both without automatic retry; 409 reloads the profile and lets the player retry. Tokens stay in memory and never appear in URLs/logs/localStorage; the picture is fetched with a bearer header and shown through a revocable object URL (SEC-08). Browser must not request machine scopes or call internal APIs. No public answer key, password/hash, machine secret or issuer administration data is returned.
