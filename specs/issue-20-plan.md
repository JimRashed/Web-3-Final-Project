# Issue #20 (P05) plan: reproducible OIDC provider and client/service identities

Parent #8 (F00), epic #7. Prerequisite P02 #17 merged via PR #70 (Keycloak approved as ADR-04).

## Goal
A reproducible local Keycloak realm that issues standard OAuth/OIDC tokens matching docs/milestone3/authorization-matrix.md: a public PKCE browser client with user scopes only, and a confidential Gameplay client with the four machine scopes, each scope mapped to its resource audience.

## File changes
| Path | Change |
|---|---|
| infra/compose.yaml | New. Keycloak 26.7.5 `start-dev --import-realm`, localhost-only ports, persistent volume, secrets from env |
| infra/oidc/quizarena-realm.json | New. Realm `quizarena`, 18 client scopes (basic, profile, 12 user, 4 machine) with audience mappers, two clients, `qa_actor` claim |
| infra/oidc/provision-demo-users.mjs | New. Creates/resets player-a and player-b from env password via admin REST |
| infra/oidc/verify-oidc.mjs | New. Automated positive/negative provider checks; no tokens/secrets printed |
| .env.example | New. Required variable names with blank secret values |
| .gitignore | Add `.env` |
| docs/milestone3/authentication.md | New. Actors, setup, endpoints, clients, scope/audience table, claims for P06, evidence |

## Decisions
- Scopes are optional client scopes so tokens carry only requested audiences; Gameplay requests one scope per destination.
- Actor kind is a signed hardcoded `qa_actor` claim per client (matrix forbids inferring from subject text).
- Implicit, password, device and CIBA grants disabled; PKCE S256 enforced; exact redirect URI.
- Secret injected via realm-import env placeholder on first import only.

## Validation
- `docker compose --env-file .env -f infra/compose.yaml up -d`; readiness on :9000.
- `node --env-file=.env infra/oidc/provision-demo-users.mjs`
- `node --env-file=.env infra/oidc/verify-oidc.mjs` must report all checks passed.
- Restart container; import skipped, users persist, checks still pass.
- `git status` shows no `.env`; grep committed files for secrets.

## Out of scope
API JwtBearer/policies (P06 #21), React auth module (P07 #22), legacy account mapping (D02 #63).
