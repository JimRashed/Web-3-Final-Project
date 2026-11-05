# #24 — Isolated per-service test hosts and a bearer-token test fixture

> Issue: #24 (P09) · Parent feature: #8 (F00) · Epic: #7 (E00) · Owner: @SuSu-Lisu
> Prerequisites #18, #19 and #21 are merged.

## Problem

Every service integration suite currently carries its **own copy** of the token helper and the
secured host factory (added with #21), and its own migration-test scaffolding (#19/#78).
Six copies of the same `TestTokens` / `SecuredApiFactory` pair is six places to fix when the
token shape changes, and the copies have already drifted in small ways.

## Scope of this change

1. New `tests/QuizArena.TestSupport/` project — test-only, holds **no production contracts**
   (allowed by the ADR, which only forbids a shared *production* assembly).
2. `EphemeralTokenIssuer` — one RSA key per suite, real signed JWTs, local
   `OpenIdConnectConfiguration` metadata, and explicit negative permutations
   (expired, no expiry, wrong issuer, wrong audience, wrong signing key, ID-token `typ`).
3. `AuthenticatedHostFactory<TProgram>` — a generic `WebApplicationFactory` that runs the real
   `JwtBearer` pipeline against that metadata, so tests never bypass the production policies.
4. Documentation of the conventions in `docs/milestone3/testing.md`.

Not in this change (follow-up commits on the same issue): migrating the six suites to the
shared fixture, and the per-test database helper. Those touch every service project and are
better done with a machine that can build.

## Design

**Token identity model** (mirrors the realm from #20):

| Claim | User tokens | Machine tokens |
|---|---|---|
| `iss` | configured realm URL | same |
| `aud` | the service audience | same |
| `sub` | player subject | service-account subject |
| `azp` | `quizarena-client` | `quizarena-gameplay` |
| `qa_actor` | `user` | `machine` |
| `scope` | requested scopes | requested scopes |
| `typ` | `Bearer` | `Bearer` |

**Host factory** binds the same `Authentication:*` settings the API validates, then replaces
only the metadata source with an in-memory `OpenIdConnectConfiguration`, so signature,
issuer, audience, lifetime and policy checks all still execute for real.

## Validation

```powershell
dotnet build tests/QuizArena.TestSupport/QuizArena.TestSupport.csproj
dotnet test  tests/QuizArena.Notification.IntegrationTests   # after migration to the shared fixture
```

Negative controls: an expired token, a token signed by a different key with the same `kid`,
a wrong-audience token and a `typ=ID` token must all produce `401`; a valid token without the
required scope must produce `403`.

## Risks

- The shared project must never reference a service project, or the isolation rule breaks.
- The fixture must not expose a "skip validation" switch; every shortcut becomes a test that
  passes for the wrong reason.
