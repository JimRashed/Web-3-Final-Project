# Security and cross-device profile decisions (SEC-A1)

Status: **Proposed — team approval pending.**

Issue [#137](https://github.com/JimRashed/Web-3-Final-Project/issues/137) (A1), parent #133 (SEC-A), tracker #132 (QA-SEC). Integration base `feature/milestone-3-layered-service-development @ c8dcc78`. Related: [authorization matrix](authorization-matrix.md), [identity contract](../../contracts/openapi/identity.yaml), [API contracts](api-contracts.md), [architecture decisions](architecture-decisions.md), [plan](../../specs/issue-137-plan.md).

Every row below is a proposal. Route names, scopes, statuses, formats and limits stay decisions until both other team members approve them on the pull request (acceptance item 5 of #137). No agent may mark a decision or reviewer as approved; the owner links the human approval from the pull request. Dependent work (#138 to #146) starts only after that link exists.

No secret value appears in this document. Never paste one into an issue, pull request or log.

## Decisions

| ID | Proposed decision | Reason / consequence | Routes to |
|---|---|---|---|
| SEC-01 | **Routes and scopes.** `GET /api/users/me/picture` and `PUT /api/users/me/picture` on the existing `/api/users/me` boundary. Read uses `profiles.read`, replace uses `profiles.write`; no new scope. User actor only (browser client `quizarena-client`, `qa_actor=user`), audience `quizarena-identity`. Statuses as declared in [identity.yaml](../../contracts/openapi/identity.yaml). Implementation note (#144, not an approval): both operations are implemented and their `x-status`/`x-implemented-by` markers are removed. | Reuses the shipped policies and Keycloak scopes, so no realm or client change is needed. The browser already requests both scopes. #144 removes the markers when it implements the endpoints. | #144 |
| SEC-02 | **Accepted formats and size limit.** PNG, JPEG and WebP only; the leading magic bytes must match the request Content-Type. SVG, GIF and any other type get 415. A Content-Type that does not match the bytes, or an empty or malformed body, gets 400. *Malformed* means the leading magic bytes match an accepted type but the format header that follows is truncated or invalid: PNG without a complete `IHDR` chunk after the 8-byte signature, JPEG without a frame header (`SOFn`) after the `FF D8` start-of-image marker, WebP without a complete `RIFF`…`WEBP` header and `VP8 `/`VP8L`/`VP8X` chunk. Pixel dimensions are not checked in this milestone. Bodies over 2,097,152 bytes (2 MiB) get 413; enforce the limit while reading, without buffering past it. The body is the raw image (not multipart). No transcoding in this milestone. | SVG can carry script and GIF adds little value; a fixed allowlist with a byte check rejects polyglot and renamed files. **Open question for the team:** whether to re-encode images to strip metadata (EXIF location) in a later milestone, and whether to add a maximum pixel-dimension limit against decompression bombs (#139 threat model, #144 implementation). | #144 |
| SEC-03 | **Profile response shape.** `Profile` gains an optional, nullable `picture` object `{contentType, updatedAt, version}`. `picture` is `null` (or absent until #144 lands) when the profile has no picture. It never contains a storage key, file path or URL. | Lets the client know whether to fetch the picture and detect replacement by `version` without exposing storage details. Whether #144 makes the property required-and-nullable is decided there. Implementation decision (#144, not an approval): `picture` is required and nullable; it is always present and `null` when there is no picture. | #143, #144 |
| SEC-04 | **Ownership.** The picture owner is resolved only from the validated `iss` (equal to the configured `Authentication:Authority`, trailing `/` trimmed) plus `sub`, mapped to `UserProfiles (Issuer, Subject)`. No client-supplied profile ID, user ID or path parameter selects the owner. One picture per profile. | Matches the shipped `/api/users/me` rule (ADR-05) and makes cross-user reads or writes impossible by construction. | #143, #141 |
| SEC-05 | **Storage interface and migration.** A Domain port, for example `IProfilePictureStore`, with get, replace and delete-by-profile; all methods async and taking a `CancellationToken`. Identity Data implements it with a SQL Server table `ProfilePictures` (columns below). One additive EF Core migration whose Down drops only the new table. No separately deployed storage service. | Keeps Domain free of EF and storage details, keeps pictures in the Identity database the service already owns, and needs no new infrastructure. Because there is no separate storage service, no storage contract test applies to #146 (D1). | #143 |
| SEC-06 | **Failure behaviour.** Validate type, magic bytes and size before any write. Replace atomically in one transaction, so a reader never sees a partial image. A storage or database outage returns 503 (problem+json). A failed PUT keeps the previous picture. | Atomic replacement plus validate-first keeps the stored picture consistent. Mapping transient SQL failures to 503 also resolves discrepancy DR-3 for Identity. Implementation note (#144, not an approval): an unknown commit outcome returns a safe 500 asking the client to reload its profile before retrying; Conflict → 409, missing profile → 404, corrupt stored row → safe 500; transient inner causes no longer turn these into 503. `PUT /api/users/me` reads picture metadata before writing the name, so a failed metadata read leaves the name unchanged. | #143, #144 |
| SEC-07 | **Concurrent replacement.** The first committer wins. A concurrent request that loses the `rowversion` check, or loses the primary-key insert race on a profile's first upload, gets 409 and may retry; it does not overwrite the winner. Sequential requests each replace the picture. `version` increases by 1 on every successful replacement. No `If-Match`/ETag in this milestone. | Two devices can upload at once without corrupting the row. ETags are deferred because CORS exposes no response headers and the client has no support for them. | #143, #144, #145 |
| SEC-08 | **Private delivery and caching.** Pictures are served only through the authenticated bearer `GET /api/users/me/picture`. Response headers: `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff`; Content-Type is the stored, validated type. No public or signed URLs and no token inside an `<img src>`. The client downloads a Blob, creates an object URL and revokes it on replace, sign-out and unmount. | Keeps pictures owner-only, keeps tokens out of URLs and logs, and prevents shared caches or a later user of the same browser from reading them. | #144, #145 |
| SEC-09 | **Per-device sign-out.** When device A signs out it clears its in-memory tokens, profile and picture object URLs, then ends its own Keycloak session. Device B stays signed in and is not affected. | Matches the per-device session model. **Residual risk:** an access token already issued stays valid for up to 300 s (realm `accessTokenLifespan`) plus 30 s clock skew; there is no revocation list. #147 (D2) verifies the two-device behaviour. | #145, #147 |
| SEC-10 | **Secret store per environment.** Local: a git-ignored `.env` file, injected as environment variables by the start script (`scripts/start-milestone3.ps1`); .NET user-secrets are optional. CI: GitHub Actions secrets. Deployment target: **undefined — open item.** | One documented place per environment. Secret values are never committed or pasted into issues, pull requests or logs. The deployment secret store is chosen when a deployment target exists. | #138 |
| SEC-11 | **Prerequisites.** #62 (documented local six-service run) blocks A2 #138. #130 (live two-player demo) and its fix PR #131 (resume shared match after sign-in) block C3 #145 and D2 #147. State on 2026-11-21: see [Prerequisites](#prerequisites). | The secret work needs the documented local run; the client picture work and the cross-device journey need a working signed-in demo. | #138, #145, #147 |
| SEC-12 | **Multi-audience browser token.** The browser requests all 12 user scopes (`src/QuizArena.Client/src/auth/session.ts:3`), so one access token carries six audiences. This is accepted by design. Each host validates its own audience and exact scope. Machine tokens carry one scope and one audience each. | Avoids six separate sign-ins. **Residual risk:** a leaked browser token works against every host for its lifetime. Recorded for the threat model. | #140, #139 |

### `ProfilePictures` table (SEC-05)

| Column | Type / rule |
|---|---|
| `ProfileId` | Primary key; foreign key to `UserProfiles.Id` with cascade delete |
| `ContentType` | string; one of `image/png`, `image/jpeg`, `image/webp` |
| `Content` | `varbinary(max)`; at most 2,097,152 bytes, enforced by the API |
| `Version` | int; starts at 1, +1 per replacement |
| `UpdatedAt` | `datetime2` (UTC) |
| `RowVersion` | `rowversion`; concurrency token (SEC-07) |

Implementation note (#143, not an approval): the store validates every row it reads and treats a corrupt row as a
server fault. If a commit's acknowledgement is lost, it re-reads the row and reports success only when the row is its
own write (matching rowversion, version, content type and content hash), so one upload never increments `version` twice.
That check uses locking reads (`READCOMMITTEDLOCK`), so it stays correct under READ_COMMITTED_SNAPSHOT. If the check itself
fails, the store reports an unknown outcome (a retry may double-count) instead of a retry-safe failure. The metadata read
checks the stored byte length (1 to 2,097,152) without loading the bytes.

## Discrepancy register

Shipped code at `c8dcc78` compared with the previous matrix, ADRs and contracts. IDs use the `DR-` prefix so they are not confused with the issue stable IDs D1 (#146) and D2 (#147). The security files under `src/QuizArena.<Service>.Api/Security/` are identical on all six hosts apart from namespaces and the `<Service>Policies.cs` file; Identity line numbers are shown.

| ID | Shipped code (file:line) | Matrix / ADR claim | Proposed resolution | Routes to |
|---|---|---|---|---|
| DR-1 | `ScopeAuthorizationHandler.cs:19-28` checks signed `azp`, `qa_actor`, a non-blank `sub` and exact scope after bearer validation. | Previous matrix required a configured service-account subject allowlist. | #140 aligns the matrix with the shipped policy. Keycloak issues the service-account subject and the signed client and actor claims. | #140, verification tracked in #146 |
| DR-2 | `POST /matches` returned 409 for an unlocked lobby before checking ownership. | The matrix requires 403 for a non-host before state validation. | SEC-B2 checks the verified host before existing-match lookup or state validation. | #141 |
| DR-3 | `QuizArena.Identity.Api/Services/IdentityExceptionHandler.cs:21-23` handles only `InvalidProfileException`; a database outage falls through to the default 500. | identity.yaml declares 503 "Dependency unavailable" for both `/api/users/me` operations. | Map transient SQL failures to 503 (as Lobby and QuestionBank do), together with SEC-06. | #143 |
| DR-4 | Lobby, Gameplay and Notification used varying titles for permission failures. | Contract examples use "Insufficient scope, actor kind or ownership". | SEC-B2 applies the canonical title and a safe detail to all authorization denials. | #141 |
| DR-5 | Lobby and Gameplay store subjects in host, member and participant records. Identity, Statistics and Notification also store issuer with subject. | ADR-05 defines identity as exact issuer plus opaque subject. | SEC-B2 records the single-issuer exception. Lobby and Gameplay pin the same configured issuer, and each store is dedicated to that issuer. Reusing those stores with a different issuer requires explicit identity migration. | #141 |
| DR-6 | Browser token requests all 12 user scopes (`session.ts:3`); realm `infra/oidc/quizarena-realm.json` adds one audience mapper per scope, giving six audiences. | Matrix implies destination-scoped tokens. | Accept by design and record as residual risk (SEC-12). | #140, #139 |
| DR-7 | identity.yaml info title "proposed P02" and description "does not yet implement this service"; matrix heading "P02". | — | Fixed in this pull request. | #137 |
| DR-8 | Statistics (`StatisticsExceptionHandler.cs:41`), Gameplay for its own database (`GameplayExceptionHandler.cs:30`) and Notification (`NotificationExceptionHandler.cs:25`) have no transient-SQL mapping, so a database outage returns 500. Lobby and QuestionBank map it to 503. | Every protected route in the six contracts declares 503 "Dependency unavailable". | Map transient SQL failures to 503 on the remaining hosts, or narrow the contracts. | Team to assign (no SEC issue owns it yet); record in #139 |
| DR-9 | `PUT /api/users/me` used media-type routing metadata that could select a 415 response ahead of scope and actor checks. | The matrix requires scope and actor checks before request validation, and the original OpenAPI contract omitted 415. | SEC-B2 validates `application/json` after route authorization and publishes the 415 response in `identity.yaml` and the contract verifier. | #141 |

## Threat-model hand-off to A3 (#139)

- Residual access-token lifetime after sign-out: up to 300 s plus 30 s skew, no revocation (SEC-09).
- Multi-audience browser token: one leaked token works against all six hosts (SEC-12, DR-6).
- Machine identity relies on the trusted realm issuing the correct `azp` and `qa_actor` claims for the service-account subject, without an independent API subject allowlist (DR-1).
- Upload attack surface: polyglot and renamed files, decompression bombs (very large pixel dimensions in a small file; no dimension limit this milestone, open question in SEC-02), embedded metadata such as EXIF location, oversized bodies and slow uploads (SEC-02).
- Private-delivery assumptions: no shared caching, no URL-embedded tokens, object URLs revoked on sign-out (SEC-08).
- Error-mapping gaps that turn outages into 500 (DR-3, DR-8).
- Configuration and secret-handling residual risks from SEC-A2 (#138): see [configuration-review.md](configuration-review.md).

## Prerequisites

State checked with `gh` on 2026-11-21:

| Prerequisite | State | Blocks |
|---|---|---|
| #62 — run the six-service application and frontend from a documented local environment | Open. Runbook and start/stop scripts delivered in #113; the clean-checkout `npm ci` fix (#127) is merged into the milestone branch. Remaining acceptance step: another team member runs the clean-checkout start and records the result. | A2 #138 |
| #130 — verify and repair the live two-player gameplay demo | Open. Its fix PR #131 ("resume shared match after sign-in") **merged** into `feature/milestone-3-layered-service-development` on 2026-11-21. The issue still needs its acceptance evidence linked and closure. | C3 #145, D2 #147 |

## Approval

| Reviewer | Approval | Evidence / date |
|---|---|---|
| JimRashed | Pending (required approver) | PR review approval of [PR #149](https://github.com/JimRashed/Web-3-Final-Project/pull/149) at `96fd09a` and `f2c5b0c` (2026-11-22); formal decision approval not recorded |
| SuSu-Lisu | Pending (required approver) | — |
| CarterBeasse | Author (n/a) | Pull request author; does not count as an approver |

Acceptance item 5 of #137 needs a comment or review from both members other than the pull request author, linked here or from the pull request, before #138 to #146 start. Automated checks and agents never supply this approval.
