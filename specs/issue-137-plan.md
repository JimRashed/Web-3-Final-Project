# Issue #137 plan: [SEC][A1] Approve QuizArena's security and picture contracts

- Issue: https://github.com/JimRashed/Web-3-Final-Project/issues/137 (parent #133, tracker #132)
- Branch: `feat/137-approve-security-and-picture-contracts`, based on `feature/milestone-3-layered-service-development` @ `c8dcc78`
- Plan status: **agent-reviewed (Dispatcher pipeline), not human-approved.** Team approval (acceptance item 5) happens only on the draft PR.
- Prior evidence: `.dispatcher/runs/137-202611211549/context.md` (Prime, PASS). I checked these Prime claims against the code:
  - `ScopeAuthorizationHandler.cs` checks azp, qa_actor, sub and exact scope. It has no subject allowlist.
  - `LobbyProblems.cs:34` uses the 403 title "Insufficient permission".
  - `EventsController.cs:50` uses the 403 title "Forbidden".
  - The two Identity contract tests read `identity.yaml`.
  - `contracts/validate-contracts.mjs` expects 30 operations, has a schema-keyword allowlist and accepts JSON request bodies only.

## 1. Summary and architecture

This is a contract and decision task. It changes no production code, authorization behaviour, migrations or other services. The PR delivers four things:

1. **Shipped security inventory.** `docs/milestone3/authorization-matrix.md` lists every public, user and machine route on all six hosts. Each row gives the issuer, audience, actor, exact scope, ownership rule and response rules that the code enforces today.
2. **Proposed picture contract.** `contracts/openapi/identity.yaml` gains `GET` and `PUT /api/users/me/picture` and a nullable `Profile.picture` property.
   - Each of these carries `"x-status": "proposed"` and `"x-implemented-by": "#144"`.
   - C2 (#144) removes the markers when it implements the endpoints.
   - The file stays JSON-compatible YAML.
3. **Decision record.** A new `docs/milestone3/security-decisions.md` holds decisions SEC-01 to SEC-12, all "Proposed — team approval pending". Its reviewer table stays Pending.
4. **Conformance tooling.** The Identity contract tests and `validate-contracts.mjs` learn to separate proposed items from implemented ones, without weakening the gate:
   - Only items that carry the marker are excluded.
   - A pinning test fixes exactly which items are proposed.
   - So the check on implemented operations is as strict as before.

Layer boundaries: the changes are limited to `contracts/`, `docs/milestone3/`, `specs/` and test helpers in `tests/QuizArena.Identity.IntegrationTests/`. Nothing under `src/` changes.

## 2. File inventory

| Action | File | Purpose |
|---|---|---|
| Modify | `contracts/openapi/identity.yaml` | Fix the stale info title and description. Add the two proposed picture operations. Add the proposed nullable `Profile.picture`, defined inline because the validator forbids `$ref` siblings. |
| Modify | `contracts/validate-contracts.mjs` | Add the picture routes to the inventory and route contracts. Support binary image media. Allow and enforce the marker keywords. Expect 32 operations instead of 30. Add new corruption probes. |
| Modify | `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileEndpointsTests.cs` | Leave marked-proposed contract operations out of the exact-operations comparison, on the contract side only. |
| Modify | `tests/QuizArena.Identity.IntegrationTests/Application/ProfileApplicationTests.cs` | Leave marked-proposed schema properties out of `ContractSchemaProperties`. Add two pinning tests that need no database. |
| Create | `docs/milestone3/security-decisions.md` | Decisions SEC-01 to SEC-12, the discrepancy register (D1 to D7), prerequisites and the Pending reviewer table. |
| Modify | `docs/milestone3/authorization-matrix.md` | Fix the heading and status. Add a token-validation section and the full shipped route table. Add the two proposed picture rows. Correct the machine-policy paragraph. Link the decision record. |
| Modify | `docs/milestone3/api-contracts.md` | Add two inventory rows marked proposed. Add an Identity paragraph on the picture proposal and a note on the marker convention. |
| Modify | `docs/milestone3/architecture-decisions.md` | Add a header link to `security-decisions.md`. The ADR rows and approval table stay unchanged. |
| Create | `specs/issue-137-plan.md` | This plan. |

**Not touched:**
- anything under `src/`
- the other five OpenAPI documents
- the client
- the CI workflows
- `specs/implementation-plan.md`
- `traceability.json`

## 3. Ordered steps

### Step 1: Inventory shipped security (read-only; feeds Steps 5 and 6)

**Files to read:**
- `src/QuizArena.<S>.Api/Security/SecurityExtensions.cs`, `ScopeAuthorizationHandler.cs` and `<S>Policies.cs`
- every controller under `src/QuizArena.<S>.Api/Controllers/`
- each `Program.cs`, for the health route and the fallback policy
- the error mappers: `LobbyProblems.cs`, Identity's `IdentityExceptionHandler.cs`, and the Gameplay and Notification problem mapping
- the Keycloak realm export; find it with Glob

**For each route, record:**
- method and path
- actor: public, user or machine
- exact scope
- audience: `quizarena-<service>`
- issuer: the configured `Authentication:Authority`, with any trailing `/` trimmed
- the ownership rule as coded
- the shipped status codes in the order they are checked

**Done when:** you have the same 30 operations as the validator inventory, and every discrepancy is confirmed with a file:line reference. That covers Prime's seven discrepancies plus any new ones you find.

### Step 2: Extend the identity contract (`contracts/openapi/identity.yaml`)

**Info block.**
- Set the title to `QuizArena identity API`.
- Rewrite the description to say three things:
  - the v1 operations are implemented by the Identity service;
  - items marked `x-status: proposed` are pending team approval and will be implemented by #144;
  - examples are JSON-compatible and contain no credentials.
- This resolves discrepancy D7.

**`Profile.properties.picture`.**
- An inline object with these keywords:
  - `nullable: true`
  - `x-status: proposed`
  - `x-implemented-by: "#144"`
  - `additionalProperties: false`
  - `required: [contentType, updatedAt, version]`
- Its properties:
  - `contentType`: string, enum `image/png`, `image/jpeg`, `image/webp`
  - `updatedAt`: string, format `date-time`
  - `version`: integer, `minimum: 1`
- Do **not** add `picture` to `Profile.required`, so existing examples and responses stay valid.
- The property carries no storage key, path or URL.

**New path `/api/users/me/picture`, operation `get`.**
- operationId `getMyPicture`.
- Security and markers:
  - security scope `profiles.read`
  - `x-policy: profiles.read`
  - `x-actor: user`
  - `x-audience: quizarena-identity`
  - `x-ownership: validated issuer/subject`
  - `x-status: proposed`
  - `x-implemented-by: "#144"`
  - `parameters: []`
- 200 response:
  - content for `image/png`, `image/jpeg` and `image/webp`
  - each content entry has schema `{type: string, format: binary}` and a placeholder string example
  - headers `Cache-Control` (example `private, no-store`) and `X-Content-Type-Options` (example `nosniff`), each with an example
- 401: problem+json plus the `WWW-Authenticate` header, as in the existing operations.
- 403, 404 (no profile or no picture), 500 and 503: problem+json, each with an example.

**Same path, operation `put`.**
- operationId `replaceMyPicture`, scope `profiles.write`, with the same set of markers.
- Request body:
  - `required: true`
  - content for the three image media types, each with the binary schema and a placeholder example
  - the description states the 2,097,152-byte (2 MiB) limit, that the magic bytes must match the Content-Type, and that SVG and GIF are rejected
- Responses:
  - 200: `application/json` Profile, with an example that includes `picture`
  - 400: empty, malformed, or Content-Type does not match the bytes
  - 401: with `WWW-Authenticate`
  - 403
  - 404: no profile yet
  - 409: a concurrent replacement committed first
  - 413: body over 2 MiB
  - 415: unsupported media type
  - 500
  - 503: storage unavailable
  - All error responses are problem+json with examples.

**Done when:** `node -e "JSON.parse(require('fs').readFileSync('contracts/openapi/identity.yaml','utf8'))"` succeeds.

### Step 3: Extend the validator (`contracts/validate-contracts.mjs`)

**Inventory and route contracts.**
- `expected.identity`: add `get /api/users/me/picture` and `put /api/users/me/picture`.
- `routeContracts.identity`:
  - `'get /api/users/me/picture': ['user','profiles.read',null,{200:'image'}]`
  - `'put /api/users/me/picture': ['user','profiles.write','image',{200:'Profile'}]`
- New constants:
  - `IMAGE_MEDIA = ['image/png','image/jpeg','image/webp']`
  - `proposedOps = {identity: {'get /api/users/me/picture': '#144', 'put /api/users/me/picture': '#144'}}`

**Image media.** Wherever a request or outcome is `'image'`:
- the content keys must equal `IMAGE_MEDIA` exactly, so adding `image/svg+xml` or `image/gif` fails;
- each media schema must be exactly `{type:'string', format:'binary'}` and must have an example.

Implement this in a dedicated helper. Do not put `binary` into the generic `schema()` format list, or it would become valid inside JSON schemas too.

Change the JSON-only request rule to "JSON unless the route contract says `'image'`". Skip the client-selected actor-id property check for binary bodies.

**Allowed keywords.** Add `x-status` and `x-implemented-by` to the `allowed` schema-keyword set.

**Marker rules.**
- An operation may have `x-status` if and only if it is listed in `proposedOps`. Its value must be `proposed`, and its `x-implemented-by` must equal the listed issue.
- Any property-level `x-status` must be `proposed`, must have `x-implemented-by`, and the property must not be in the parent's `required` list.

**Required picture outcomes.**
- PUT must declare 400, 404, 409, 413, 415 and 503.
- GET must declare 404.

**Operation count.** Change the check to `operations===32` with the message `Expected 32 operations (30 implemented + 2 proposed)`.

**New corruption probes.** Each one must make validation fail:
- (a) the PUT picture operation loses its `x-status`;
- (b) `image/svg+xml` is added to the PUT request content;
- (c) the GET picture operation is made public;
- (d) the PUT 413 response is deleted;
- (e) `getMyProfile` is marked proposed;
- (f) `picture` is added to `Profile.required` while still marked proposed.

**Done when:** `node contracts/validate-contracts.mjs` prints PASS with `operations: 32`, `perService.identity: 8` and `corruptionProbes: 18`.

### Step 4: Adjust the Identity contract tests (no production change)

**`ProfileEndpointsTests.cs`.**
- Add a helper: `private static bool IsProposed(JsonElement node) => node.TryGetProperty("x-status", out var s) && s.GetString() == "proposed";`
- Change `Operations(JsonElement openApi, bool excludeProposed = false)` so it drops operations where `IsProposed` is true, but only when the flag is set.
- In `OpenApiDocument_InDevelopment_ListsExactlyTheContractOperations`, compare against `Operations(ReadContract(), excludeProposed: true)`.
- The generated document is never filtered. This test needs SQL Server or LocalDB through `IdentityApiFactory`.

**`ProfileApplicationTests.cs`.** This file needs no database.
- `ContractSchemaProperties(string schema, bool excludeProposed = true)` skips any property whose value has `x-status == "proposed"`.
- `ProfileResponse_SerializedAsWebJson_MatchesContractProfileSchema` and `UpdateProfileRequest_HasExactlyTheContractFields` keep their bodies.
- New test `ContractDocument_ProposedOperations_AreExactlyThePictureOperationsWithFollowUpMarker`. It reads `identity.yaml` and asserts:
  - the proposed operations are exactly `GET /api/users/me/picture` and `PUT /api/users/me/picture`;
  - each has `x-implemented-by == "#144"`;
  - `GET /api/users/me` and `PUT /api/users/me` are not proposed.
  - This proves the exclusion covers only marked items, and that an implemented operation cannot be hidden by marking it.
- New test `ProfileContract_ProposedProperties_AreOnlyNullableOptionalPicture`. It asserts:
  - the only proposed Profile property is `picture`;
  - `picture` has `nullable: true`;
  - `picture` is not in `required`;
  - `picture` has `x-implemented-by == "#144"`;
  - the unfiltered call returns `ProfileResponse`'s four fields plus `picture`.
- Use the existing Arrange/Act/Assert comments and FluentAssertions style.

**Done when:** both classes compile, the tests that need no database pass, and `ProfileEndpointsTests` passes where SQL Server is available.

### Step 5: Write the decision record (`docs/milestone3/security-decisions.md`, new file)

**Header.**
- Title: `# Security and cross-device profile decisions (SEC-A1)`.
- Status line: `Status: **Proposed — team approval pending.**`
- Links: #137, #133, #132, base `c8dcc78`, the authorization matrix, identity.yaml and the ADRs.
- State that agents must not mark any decision approved, and that approval is linked from the PR.

**Decision table.** Columns: ID, Proposed decision, Reason / consequence, Routes to.

**SEC-01: Routes and scopes.** Routes to #144.
- `GET` and `PUT /api/users/me/picture`, on the existing `/api/users/me` boundary.
- Scopes `profiles.read` and `profiles.write`. No new scope.
- User actor only. Audience `quizarena-identity`.
- Statuses as declared in identity.yaml.
- Route names, scopes and statuses stay proposals until approved.

**SEC-02: Accepted formats and size limit.** Routes to #144.
- PNG, JPEG and WebP only. The magic bytes must match the Content-Type.
- SVG, GIF and anything else get 415. A Content-Type that does not match the bytes gets 400.
- Bodies over 2,097,152 bytes get 413. Enforce the limit without buffering past it.
- Raw request body, not multipart.
- No transcoding in this milestone. Whether to re-encode to strip metadata is an open question for the team.

**SEC-03: Profile response shape.** Routes to #143 and #144.
- A nullable `picture` object: `{contentType, updatedAt, version}`.
- No storage keys, paths or URLs.

**SEC-04: Ownership.** Routes to #143 and #141.
- Ownership comes only from the validated `iss` (equal to the configured Authority) plus `sub`, mapped to `UserProfiles (Issuer, Subject)`.
- No client-supplied IDs.
- One picture per profile.

**SEC-05: Storage interface and migration.** Routes to #143.
- A Domain port, for example `IProfilePictureStore`, with get, replace and delete-by-profile. All methods are async and take a CancellationToken.
- Identity Data implements it with a SQL Server table `ProfilePictures`:

  | Column | Type / rule |
  |---|---|
  | `ProfileId` | PK; FK to `UserProfiles.Id` with cascade delete |
  | `ContentType` | string |
  | `Content` | `varbinary(max)` |
  | `Version` | int |
  | `UpdatedAt` | `datetime2` |
  | `RowVersion` | `rowversion` |

- One additive EF migration. Its Down drops only the new table.
- No separately deployed storage service, so a D1 storage contract test does not apply.

**SEC-06: Failure behaviour.** Routes to #143 and #144.
- Validate before any write.
- Replace atomically in one transaction, so no reader ever sees a partial image.
- Storage or database outage returns 503. This also resolves discrepancy D3.
- A failed PUT keeps the previous picture.

**SEC-07: Concurrent replacement.** Routes to #143, #144 and #145.
- The last committed writer wins.
- A `rowversion` conflict returns 409 to the losing request, which may retry.
- `version` goes up by 1 on every replacement.
- No `If-Match` or ETag in this milestone: CORS exposes no headers and the client has no support for them.

**SEC-08: Private delivery and caching.** Routes to #144 and #145.
- Pictures are served only through an authenticated bearer GET.
- Response headers: `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff`. The Content-Type comes from the stored, validated type.
- No public or signed URLs, and no token inside an `<img src>`.
- The client downloads a Blob and creates an object URL. It revokes the URL on replace, sign-out and unmount.

**SEC-09: Per-device sign-out.** Routes to #145 and D2.
- When device A signs out, it clears its in-memory tokens, profile and blob URLs, then ends its own Keycloak session.
- Device B is not affected.
- Residual risk: an access token already issued stays valid for up to 300 s plus 30 s of clock skew. There is no revocation list.
- D2 verifies this behaviour.

**SEC-10: Secret store per environment.** Routes to #138.
- Local: a git-ignored `.env`, injected as environment variables by the start script. User-secrets are optional.
- CI: GitHub Actions secrets.
- Deployment target: undefined; recorded as an open item.
- Never commit secret values or paste them into issues or PRs.

**SEC-11: Prerequisites.**
- #62 (documented local six-service run) blocks A2 #138.
- #130 and PR #131 (live two-player demo, resume after sign-in) block C3 #145 and D2.

**SEC-12: Multi-audience browser token.** Routes to #140 and #139.
- The browser requests all 12 user scopes, so its token carries six audiences. This is accepted by design.
- Each host validates its own audience and exact scope.
- Machine tokens carry one scope and one audience each.
- Recorded as a residual risk.

**Discrepancy register.** Columns: ID, Shipped code (file:line), Matrix/ADR claim, Proposed resolution, Routes to.

| ID | Discrepancy | Proposed resolution | Routes to |
|---|---|---|---|
| D1 | No machine subject allowlist; the handler checks azp, qa_actor, sub and scope only | Add a configured service-account `sub` allowlist, or amend the matrix | #140, verified in #146 |
| D2 | Gameplay `POST /matches` returns 409 for an unlocked lobby before 403 for a non-host | Check 403 before 409 | #141 |
| D3 | Identity returns 500 on a database outage; the contract says 503 | Map transient SQL failures to 503 | #143 |
| D4 | 403 titles differ: Lobby "Insufficient permission", Notification `GET /events` "Forbidden" | Use the uniform title "Insufficient scope, actor kind or ownership" | #141 |
| D5 | Lobby and Gameplay ownership keys use `sub` only; ADR-05 says the issuer is included | Include the issuer, or record a single-issuer exception | #141 |
| D6 | Browser tokens with multiple audiences are accepted | See SEC-12 | #140, #139 |
| D7 | Stale "P02 proposed" wording in identity.yaml and the matrix heading | Fixed in this PR | #137 (A1) |

**Threat-model hand-off to A3 #139.** List: residual token lifetime, multi-audience tokens, the upload attack surface (polyglot files, decompression bombs, metadata), and the private-delivery assumptions.

**Approval table.**
- Reviewers JimRashed, CarterBeasse and SuSu-Lisu, all `Pending`, evidence `—`.
- Add a note: acceptance item 5 needs a comment or review from both other members on the PR before #138 to #146 start.

**Done when:** every SEC and D entry names a follow-up issue, and the word "Approved" does not appear as a status.

### Step 6: Update the authorization matrix (`docs/milestone3/authorization-matrix.md`)

1. Change the heading to `# QuizArena authorization matrix`.
2. Replace the status line with: "Shipped routes verified against code at `c8dcc78` (#137). Picture rows proposed; team approval pending. See [security decisions](security-decisions.md)."
3. Add a section "Token validation (all six hosts)" that describes what `SecurityExtensions.cs` does:
   - issuer equals the Authority with any trailing `/` trimmed;
   - audience is validated; signature is RS256; a lifetime is required, with 30 s skew; `typ` must be Bearer;
   - 401 returns problem+json with `WWW-Authenticate: Bearer` (or `Bearer error="invalid_token"`);
   - 403 returns problem+json;
   - startup fails fast when required configuration is missing;
   - the fallback policy requires an authenticated user;
   - CORS allows GET, POST, PUT and DELETE with the Authorization and Content-Type headers.
4. Rewrite the actor-policy paragraph to match the code: azp plus `qa_actor` plus `sub` plus exact scope. Reference D1 instead of claiming a subject allowlist exists.
5. Rebuild the table with these columns: Service, Method / route, Actor, Audience, Exact scope, Ownership / resource condition, Shipped responses (check order), Notes.
   - State the issuer once above the table.
   - Fill all 30 shipped rows from Step 1.
   - Add the two picture rows with **Proposed (#144)** in Notes.
   - Cross-reference D2, D4 and D5 on the affected rows.
6. Keep the evaluation-order and client-handling sections. Where the code differs, note the difference instead of silently rewriting the target. Add 413 and 415 to the client handling, marked proposed.

**Done when:** the table has 32 rows, the 30 shipped rows match the validator inventory, and the decision-record link resolves.

### Step 7: Update the API contracts index and ADR link

`docs/milestone3/api-contracts.md`:
- Add these inventory rows:

  | Service | Route | Operation | Statuses | Status |
  |---|---|---|---|---|
  | identity | GET `/api/users/me/picture` | getMyPicture | 200, 401, 403, 404, 500, 503 | proposed, #144 |
  | identity | PUT `/api/users/me/picture` | replaceMyPicture | 200, 400, 401, 403, 404, 409, 413, 415, 500, 503 | proposed, #144 |

- Add a short "Picture (proposed)" paragraph under Identity that links SEC-01 to SEC-08.
- Add one sentence on the marker convention: `x-status: proposed` plus `x-implemented-by`; C2 removes both.

`docs/milestone3/architecture-decisions.md`:
- Add `[security decisions](security-decisions.md)` to the header link list.
- Leave the ADR rows and the approval table untouched.

**Done when:** the relative links resolve and the operation IDs match identity.yaml.

### Step 8: Validate

Run the commands in Section 6. Record the actual results in the PR body. Do not quote historical counts.

## 4. Acceptance coverage

| # | Acceptance criterion (#137) | Steps | Evidence |
|---|---|---|---|
| 1 | Review and record the six hosts' public, user and machine routes with issuer, audience, actor, exact scope, ownership and response rules | 1, 6, plus the D register in 5 | Matrix with 32 rows, token-validation section, discrepancy register |
| 2 | Update identity.yaml and the matrix with self-profile read, picture upload/replacement and private retrieval, profile shape, error statuses, accepted formats and byte limit, on the `/api/users/me` boundary, using the proposal `GET/PUT /api/users/me/picture` with `profiles.read`/`profiles.write`, with decisions open until approved | 2, 3, 4, 6, 7; SEC-01 to SEC-03 in 5 | identity.yaml diff with `x-status: proposed`, validator PASS (32 operations), contract tests, picture rows in the matrix |
| 3 | Agree on issuer/subject ownership, storage interface and migration, failure and concurrent replacement behaviour, private delivery and caching, and per-device sign-out | 5 (SEC-04 to SEC-09) | security-decisions.md, recorded as proposals. "Agree" is completed through item 5 |
| 4 | Record the approved secret-store choice and prerequisite issues | 5 (SEC-10, SEC-11) | security-decisions.md, proposed until approved |
| 5 | Link team approval (a comment or review from both other members) before dependent work starts | **Human-only, pending.** The agent prepares the Pending reviewer table and a PR checklist line only | Reviews or comments from both other members on the draft PR, linked by the owner |

## 5. Testing strategy

**Static contract checker.** `node contracts/validate-contracts.mjs` with the extended inventory and six new corruption probes (Step 3). The probes cover:
- a removed marker;
- a forbidden media type;
- a picture route made public;
- a missing 413 response;
- a marker spoofed onto an implemented operation;
- a proposed property made required.

**xUnit and FluentAssertions** in the existing `tests/QuizArena.Identity.IntegrationTests` project:

| Test | Change | Needs a database |
|---|---|---|
| `OpenApiDocument_InDevelopment_ListsExactlyTheContractOperations` | Modified to exclude proposed operations on the contract side | Yes (SQL Server or LocalDB) |
| `ProfileResponse_SerializedAsWebJson_MatchesContractProfileSchema` | Body unchanged; now uses the filtering helper | No |
| `UpdateProfileRequest_HasExactlyTheContractFields` | Unchanged | No |
| `ContractDocument_ProposedOperations_AreExactlyThePictureOperationsWithFollowUpMarker` | New | No |
| `ProfileContract_ProposedProperties_AreOnlyNullableOptionalPicture` | New | No |

**Manual and static doc checks.**
- All relative links resolve.
- No "Approved" status appears in the decision or ADR tables.
- No secret values appear in the new or changed docs.
- The matrix has 32 rows.

**No new HTTP or persistence tests.** Runtime behaviour does not change. Runtime picture tests belong to #143, #144 and #145.

## 6. Validation commands

Run these from `C:/Web-3-Final-Project-wt/137`:

```
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal
node contracts/validate-contracts.mjs
```

**Environment.**
- You need the .NET 10 SDK and Node.
- The `*.IntegrationTests` projects that use database factories need SQL Server LocalDB `(localdb)\MSSQLLocalDB`, or a server set in `QUIZARENA_TEST_SQLSERVER`. This includes `ProfileEndpointsTests` through `IdentityApiFactory`, the Identity persistence tests, and the other services' integration tests.
- If LocalDB is unavailable, report the database-dependent failures as environment-blocked. Still run the tests that need no database: `dotnet test tests/QuizArena.Identity.IntegrationTests --no-build --filter "FullyQualifiedName~ProfileApplicationTests"`.
- `validate-contracts.mjs` is not run in CI. Run it by hand and paste its JSON output into the PR.

## 7. Risks and out of scope

**Risks.**

| Risk | Mitigation |
|---|---|
| The conformance gate gets weaker | Filtering applies only to the contract side and only to marked items. The pinning tests and probe (e) fail if an implemented operation is marked proposed. |
| `format: binary` leaks into JSON schemas | It is accepted only through the image-media helper. |
| The picture property becomes required too early | It stays optional while proposed (probe f). Whether C2 makes it required and nullable is a decision for #144. |
| The matrix drifts from the code | Shipped rows come from Step 1 with file:line evidence. Where the code and the target differ, both are recorded in the D register. |
| Unverified assumptions | Two-device Keycloak end-session behaviour (SEC-09) is verified later in D2. The deployment secret store is still undefined (open item in SEC-10). |
| Implied approval | Agents must not fill in approval tables or describe decisions as approved. Item 5 stays open until humans act. |

**Blockers.** None for implementation. Acceptance item 5 is the expected human gate: the issue cannot close, and #138 to #146 cannot start, until it is met.

**Out of scope.**
- Any change under `src/`: the picture endpoints and storage, the migration, the 503 mapping, 403 ordering and titles, issuer ownership keys, and a machine allowlist.
- Client UI.
- CI wiring for the validator (B3 #142).
- Configuring secrets (A2 #138).
- The threat model (A3 #139).
- The other five OpenAPI documents.
- Merging, or closing the issue or milestone.
