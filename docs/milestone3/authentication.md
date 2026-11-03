# Milestone 3 authentication (P05)

Issue [#20](https://github.com/JimRashed/Web-3-Final-Project/issues/20), parent #8, epic #7. Implements the provider side of [ADR-04/ADR-05](architecture-decisions.md) and the [authorization matrix](authorization-matrix.md). API token validation is P06 (#21); the React OIDC session is P07 (#22).

## OAuth actors

| OAuth role | QuizArena component |
|---|---|
| Resource owner | The player |
| Authorization server / OpenID provider | Keycloak realm `quizarena` (`infra/`) |
| Public client | React app, client `quizarena-client` (authorization code + PKCE S256) |
| Confidential client | Gameplay service, client `quizarena-gameplay` (client credentials) |
| Resource servers | Identity, Statistics, QuestionBank, Lobby, Gameplay, Notification APIs (audience `quizarena-<service>`) |

Keycloak owns registration, sign-in, password recovery and credentials. QuizArena never receives a player's password and never mints tokens.

## Local setup

Prerequisites: Docker Desktop (Compose v2) and Node 22+.

```powershell
Copy-Item .env.example .env   # then fill in the three blank values; .env is git-ignored
docker compose --env-file .env -f infra/compose.yaml up -d
node --env-file=.env infra/oidc/provision-demo-users.mjs
node --env-file=.env infra/oidc/verify-oidc.mjs
```

- Keycloak 26.7.5 (pinned) runs on http://localhost:8080; health/readiness on http://localhost:9000/health/ready. Ports bind to 127.0.0.1 only.
- The realm is imported from `infra/oidc/quizarena-realm.json` on first start. Later starts log "already exists. Import skipped"; data persists in the `quizarena_keycloak-data` volume across restarts.
- The Gameplay client secret is substituted from `QUIZARENA_GAMEPLAY_CLIENT_SECRET` at first import only. To rotate it, change it in the admin console (or recreate the volume) and update every environment that uses it.
- Demo players `player-a` and `player-b` are created by the provisioning script with `QUIZARENA_DEMO_PASSWORD`. Re-running it resets their passwords; it never prints them.
- Admin console: http://localhost:8080/admin with the bootstrap credentials from `.env`.
- Full reset (destroys local Keycloak data): `docker compose --env-file .env -f infra/compose.yaml down -v`.

| Endpoint | URL |
|---|---|
| Issuer / authority | http://localhost:8080/realms/quizarena |
| Discovery metadata | http://localhost:8080/realms/quizarena/.well-known/openid-configuration |
| JWKS | http://localhost:8080/realms/quizarena/protocol/openid-connect/certs |
| Authorization | http://localhost:8080/realms/quizarena/protocol/openid-connect/auth |
| Token | http://localhost:8080/realms/quizarena/protocol/openid-connect/token |

HTTP is Development-only (`start-dev`, `sslRequired: external`). Deployed environments need HTTPS, a production database, `start` mode, their own hostname and exact redirect URIs.

## Clients

| Setting | `quizarena-client` (browser) | `quizarena-gameplay` (machine) |
|---|---|---|
| Type | Public, no secret | Confidential, client secret from environment |
| Grants | Authorization code only | Client credentials only |
| PKCE | Required, `S256` only | n/a |
| Implicit / password / device / CIBA | Disabled | Disabled |
| Redirect URIs | `http://localhost:5173/auth/callback` (exact) | none |
| Web origins (CORS) | `http://localhost:5173` | none |
| Post-logout redirect | `http://localhost:5173/` | n/a |
| Optional scopes | the 12 user scopes below | the 4 machine scopes below |
| `qa_actor` claim | `user` | `machine` |

`fullScopeAllowed` is off for both clients. Refresh tokens rotate (`revokeRefreshToken`, no reuse). Access tokens live 5 minutes.

## Scopes and audiences

Each scope is a Keycloak client scope with an audience mapper, so a token only carries the audiences of the scopes actually granted.

| Scope | Audience | Granted to |
|---|---|---|
| profiles.read, profiles.write | quizarena-identity | browser |
| stats.read, leaderboard.read | quizarena-statistics | browser |
| questions.read | quizarena-questionbank | browser |
| lobbies.read, lobbies.write | quizarena-lobby | browser |
| matches.read, matches.write | quizarena-gameplay | browser |
| events.read, preferences.read, preferences.write | quizarena-notification | browser |
| results.write | quizarena-statistics | Gameplay machine only |
| questions.play | quizarena-questionbank | Gameplay machine only |
| lobbies.verify | quizarena-lobby | Gameplay machine only |
| events.write | quizarena-notification | Gameplay machine only |

- The browser requests user scopes explicitly in its authorization request, e.g. `openid profiles.read profiles.write stats.read …`. Requesting any machine scope makes Keycloak reject the whole request with `invalid_scope`.
- Gameplay requests **one scope per destination** (e.g. `scope=results.write` for Statistics) so each machine token carries exactly one audience. Requesting a user scope returns `invalid_scope`.

## Claims APIs rely on (input for P06)

| Claim | Meaning |
|---|---|
| `iss` | Must equal the configured authority exactly |
| `aud` | Must contain the API's own `quizarena-<service>` audience |
| `scope` | Space-delimited; match exact tokens, never substrings |
| `sub` | Opaque, case-sensitive subject. For users this is the player identity; for Gameplay it is its service-account user |
| `azp` | Client that obtained the token: `quizarena-client` or `quizarena-gameplay` |
| `qa_actor` | Hardcoded per client, signed: `user` or `machine`. Lets APIs distinguish actors without parsing `sub` |
| `preferred_username` | Present with the default `profile` scope; may prefill a display name, never an identity key |

Per the matrix: user policies require `azp=quizarena-client` and `qa_actor=user`; machine policies require the exact scope, `azp=quizarena-gameplay` and `qa_actor=machine`. ID tokens have `aud=quizarena-client`, so they fail every API audience check. Validate signature (RS256 via JWKS), issuer, audience, `exp`/`nbf` with framework JwtBearer; do not hand-decode.

## Verification evidence

`infra/oidc/verify-oidc.mjs` checks the running realm and prints PASS/FAIL without printing tokens or secrets. Result on 2026-11-03 against Keycloak 26.7.5: **24/24 passed**, including:

- discovery issuer and RS256 JWKS;
- each machine token has exactly one audience, its scope, `azp=quizarena-gameplay`, `qa_actor=machine` and a valid signature;
- machine client refused user scopes (`invalid_scope`) and a wrong secret (401);
- browser client refused password grant, client credentials, implicit flow, missing PKCE, `plain` PKCE, machine scopes and an unregistered redirect URI;
- a full PKCE S256 sign-in as `player-a` returns an access token with all six audiences, all 12 user scopes, no machine scopes, `azp=quizarena-client`, `qa_actor=user` and a valid signature;
- a wrong PKCE verifier is refused (`invalid_grant`); the ID token's audience is only the client;
- requesting just `stats.read` yields only the `quizarena-statistics` audience.

After `docker compose restart` the realm import is skipped, demo users persist and the checks still pass 24/24.

## API token validation (P06, #21)

Every service Api has an identical `Security/` folder (no shared assembly, per ADR-01) wired in `Program.cs`:

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddQuizArenaSecurity(builder.Configuration);
// ...
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseQuizArenaSecurity();   // CORS -> authentication -> authorization
app.MapGet("/health", ...).AllowAnonymous();
app.MapControllers();
```

| Piece | Behavior |
|---|---|
| JwtBearer | Discovery/JWKS from `Authentication:Authority`; validates RS256 signature, exact issuer, own audience, lifetime (exp required, 30 s skew); `MapInboundClaims=false` so `sub`, `scope`, `azp` keep their names. Rejects any token whose `typ` is not `Bearer` (ID/refresh tokens) |
| Fallback policy | Every endpoint requires an authenticated user unless marked `AllowAnonymous` (only `/health`) |
| Named policies | `<Service>Policies` constants equal the scope strings in the matrix. Each requires the exact scope (space-delimited `scope` or `scp` array, ordinal, never substring) **and** the actor: user = `azp=quizarena-client` + `qa_actor=user`; machine = `azp=quizarena-gameplay` + `qa_actor=machine` |
| Actor | `User.GetAuthenticatedActor()` returns `(Issuer, Subject, Kind)` from validated claims only. Endpoints use it for ownership checks; never take the acting user from body, query or headers |
| Errors | 401 `application/problem+json` with `WWW-Authenticate: Bearer`; 403 problem for missing scope/actor; both include `traceId` |
| CORS | Policy `frontend` from `Cors:AllowedOrigins` (Development: `http://localhost:5173`); preflight succeeds without a token |
| Startup | Fails fast if `Authentication:Authority` (absolute URI), `Audience`, `BrowserClientId` or `MachineClientId` is missing. `appsettings.json` leaves Authority blank; `appsettings.Development.json` points at local Keycloak with `RequireHttpsMetadata=false` |

Usage in a future endpoint task (e.g. #35):

```csharp
[HttpGet("api/players/{userId}/stats")]
[Authorize(Policy = StatisticsPolicies.StatsRead)]
public async Task<IActionResult> GetStats(string userId, CancellationToken ct)
{
    var actor = User.GetAuthenticatedActor();
    if (!string.Equals(actor.Subject, userId, StringComparison.Ordinal)) return Forbid();
    ...
}
```

### Security test matrix

`tests/QuizArena.<Service>.IntegrationTests/Security/` runs the real pipeline and real JwtBearer handler through `SecuredApiFactory`, which only swaps Keycloak discovery for local metadata holding an ephemeral RSA key, and adds a test-only probe controller with one endpoint per policy. For **every policy** of the service:

- 200 with the required scope and actor (space-delimited `scope` and `scp` array);
- 401 + problem + `WWW-Authenticate: Bearer` for: missing, malformed, bad signature, unknown key, expired, no `exp`, wrong issuer, wrong audience, `typ=ID`;
- 403 + problem for: no scope, unrelated scope, scope with suffix, scope prefix, wrong `qa_actor`, wrong `azp`, the other actor's token carrying the scope.

Plus: the response subject comes from the token even with spoofed `?userId=` and `X-User-Id`; unattributed endpoints need a token (fallback policy); `/health` returns 200 even with a junk bearer header; CORS preflight is allowed for `http://localhost:5173` and not for other origins.

Results on 2026-11-03: Identity 43, Statistics 61, QuestionBank 43, Lobby 61, Gameplay 43, Notification 79 tests passed. Mutation check on Identity: substring scope matching, disabled audience validation and accepting `typ=ID` each made tests fail. Live check against Keycloak 26.7.5 with the Statistics API: real `quizarena-statistics` token authenticated (404, no business route yet), `quizarena-lobby` token 401, tampered signature 401, `/health` 200.

## Not in this issue

- React sign-in/sign-out, in-memory token storage and refresh handling (`src/QuizArena.Client/src/auth/`): P07 (#22). The client project does not exist yet; use `VITE_OIDC_AUTHORITY=http://localhost:8080/realms/quizarena` and `VITE_OIDC_CLIENT_ID=quizarena-client`.
- Legacy account mapping to Keycloak subjects: D02 (#63), per [identity-migration.md](identity-migration.md).
