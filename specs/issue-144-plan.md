# Issue #144 plan: validate uploads and serve owner-only image content

Status: **agent-reviewed** plan produced by the Dispatcher Plan stage. Not human-approved; final human review happens
on the draft PR. SEC-01..SEC-08 in `docs/milestone3/security-decisions.md` are still pending team approval.

Base: `feature/milestone-4-security-and-cross-device-profiles` @ `312271e` (branch `feat/144-picture-api`).
Issue: https://github.com/JimRashed/Web-3-Final-Project/issues/144 (C2, parent #135; unblocks #145, #147).

## 1. Summary and architecture

Ship the two contract operations that #143 prepared behind `IProfileApplication`:

- `GET /api/users/me/picture` (`profiles.read`, user actor): the caller's own picture bytes with the stored validated
  `Content-Type`; 404 when the caller has no profile or no picture.
- `PUT /api/users/me/picture` (`profiles.write`, user actor): raw body (not multipart), PNG/JPEG/WebP only, at most
  `ProfilePicture.MaxContentLength` (2,097,152) bytes, magic bytes must match the Content-Type; atomic replace through
  the existing `IProfilePictureStore.ReplaceAsync`; returns the updated `Profile` (with `picture`).

Check order on PUT (contract and authorization matrix): **auth (401/403) → 415 → 413 → 400 → 404 → 409 → 200**;
a transient storage failure is 503 at any point after auth. Owner is always the validated issuer + subject
(`User.GetAuthenticatedActor()`); no id, key, path or URL is read from the request.

Every response of the two picture routes carries `Cache-Control: private, no-store` and
`X-Content-Type-Options: nosniff` (200, 401, 403, 404, 413, 415, 400, 409, 500, 503).

Layer boundaries (unchanged dependency direction, enforced by `tests/QuizArena.ArchitectureTests`):

| Layer | Responsibility in #144 |
| --- | --- |
| `QuizArena.Identity.Domain` | New pure `ProfilePictureFormat.Validate(contentType, ReadOnlySpan<byte>)` header checks; no NuGet. Entity, store port and exceptions unchanged. |
| `QuizArena.Identity.Data` | No change (store and migration come from #143). |
| `QuizArena.Identity.Api` | Controller actions, `RequireImageContentType` resource filter, capped body reader, picture response-header middleware, DTO change, application changes, exception mapping. |
| `contracts/` + `docs/milestone3/` | Remove proposed markers; `picture` required + nullable; validator and docs updated. |

Design decisions adopted from the Prime brief (Dispatcher-confirmed, recorded here as implementation decisions):

1. **415 filter, not `[Consumes]`** (DR-9): `RequireImageContentTypeAttribute : Attribute, IResourceFilter,
   IApiRequestMetadataProvider`, mirroring `RequireJsonContentTypeAttribute`. Resource filters run after the
   authorization middleware, so a token without the grant gets 401/403, never 415/413.
2. **413 by a capped async read** in the action: `Content-Length > limit` → 413 immediately without reading;
   otherwise read at most `limit + 1` bytes from `Request.Body`; more than `limit` → 413. Covers chunked bodies.
   No `[RequestSizeLimit]` (it would throw from Kestrel before our problem response and is not honoured by TestServer).
3. **400 by a Domain validator**: header-level structure checks only (no CRC, no pixel dimensions, no decoding).
   Kept separate from the `ProfilePicture` entity, whose #143 tests use dummy bytes and whose rehydration must not
   reject stored rows.
4. **Contract `Profile.picture` becomes required and nullable** (`"picture": null` when none). Amend SEC-03 as a
   #144 implementation decision (not a team approval). The client PR #151 already treats absent and null as null.
5. **Exception mapping** in `IdentityExceptionHandler` before the 503 walk: Conflict → 409, NotFound → 404,
   Corrupt → safe 500, CommitOutcomeUnknown → safe 500 with "reload your profile before retrying" detail. Wrapped
   transient SQL inner exceptions must no longer turn these into 503 (current bug: the 503 walk follows
   `InnerException`). Record the outcome-unknown → 500 choice as an implementation note under SEC-06.

## 2. File inventory

Create (production):

| File | Purpose |
| --- | --- |
| `src/QuizArena.Identity.Domain/Entities/ProfilePictureFormat.cs` | `public static class ProfilePictureFormat` with `Validate(string contentType, ReadOnlySpan<byte> content)`; throws `InvalidProfileException("content", ...)`. |
| `src/QuizArena.Identity.Api/Controllers/RequireImageContentTypeAttribute.cs` | Resource filter (415) + OpenAPI request metadata; static `TryGetImageMediaType(string? header, out string mediaType)` shared with the controller. |
| `src/QuizArena.Identity.Api/Controllers/PictureRequestBody.cs` | `internal static` capped async reader: `Task<byte[]?> ReadAsync(HttpRequest, int limit, CancellationToken)` (null = over limit). |
| `src/QuizArena.Identity.Api/Controllers/PrivatePictureResponseAttribute.cs` | Endpoint-metadata marker plus `internal static UsePrivatePictureHeaders(this IApplicationBuilder)` middleware that registers `Response.OnStarting` to set both headers for marked endpoints. |
| `src/QuizArena.Identity.Api/Contracts/ProfilePictureResponse.cs` | `public sealed record ProfilePictureResponse(string ContentType, DateTime UpdatedAt, int Version)` + `From(ProfilePictureInfo)`. |

Modify (production):

| File | Change |
| --- | --- |
| `src/QuizArena.Identity.Api/Controllers/ProfilesController.cs` | Add `GetMyPicture` and `ReplaceMyPicture` actions. |
| `src/QuizArena.Identity.Api/Contracts/ProfileResponse.cs` | Add `ProfilePictureResponse? Picture`; `From(PlayerProfile, ProfilePictureInfo?)`. |
| `src/QuizArena.Identity.Api/Services/IProfileApplication.cs` | `ReplaceMyPictureAsync` returns `Task<ProfileResponse>`; doc comments (format validation, 400/404/409). |
| `src/QuizArena.Identity.Api/Services/ProfileApplication.cs` | Format validation before lookup; `GetMeAsync`/`UpsertMeAsync` include picture info via `GetInfoAsync`. |
| `src/QuizArena.Identity.Api/Services/IdentityExceptionHandler.cs` | 404/409/500 mappings before the 503 walk; walk stops on `IdentityDomainException`. |
| `src/QuizArena.Identity.Api/Program.cs` | `app.UsePrivatePictureHeaders()` right after `app.UseExceptionHandler()`. |
| `src/QuizArena.Identity.Domain/Exceptions/ProfilePictureCommitOutcomeUnknownException.cs` | Doc comment only: replace "Not mapped to an HTTP status here (#144)" with the 500 mapping. |
| `contracts/openapi/identity.yaml` | Remove `x-status`/`x-implemented-by` (GET ~334-335, PUT ~494-495, `Profile.picture` ~1015-1016); drop "Proposed" from info description (line 6) and operation/property descriptions; add `picture` to `Profile.required`; add `"picture": null` to the GET `/me` 200 example (~line 51) and PUT `/me` 200 example (~line 183). |
| `contracts/validate-contracts.mjs` | `proposedOps = {}`; message `'Expected 32 operations'`; rework probes (see Step 8). Keep marker machinery, `requiredOutcomes`, `requiredHeaders`, `IMAGE_MEDIA`. |
| `docs/milestone3/api-contracts.md` | Lines 11, 12 (drop "proposed, #144"), 42 (no proposed items remain), 54 (implemented; required + nullable `picture`). |
| `docs/milestone3/authorization-matrix.md` | Lines 3, 50, 51, 81 (32 shipped rows), and picture mentions near 87/93/110 so the rows read as shipped. |
| `docs/milestone3/security-decisions.md` | SEC-03 text (required + nullable, #144 implementation decision); SEC-06 implementation note (outcome unknown → safe 500, no inner text, reload before retry); SEC-01 "proposed" wording only where it claims "not implemented". Header status stays "team approval pending". |
| `docs/milestone3/threat-model.md` | Rows 47-49 status "Proposed" → "Implemented (#144); approval pending", with test references; line 15/66 "proposed" wording where it states not implemented. |

Create (tests):

| File | Purpose |
| --- | --- |
| `tests/QuizArena.Identity.UnitTests/Domain/ProfilePictureFormatTests.cs` | Pure Domain format tests (xUnit + FluentAssertions). |
| `tests/QuizArena.Identity.UnitTests/Domain/PictureSamples.cs` | Minimal valid PNG/JPEG/WebP (VP8, VP8L, VP8X) byte builders + padding helper. |
| `tests/QuizArena.Identity.IntegrationTests/Endpoints/PictureSamples.cs` | Same minimal samples for API tests (duplicated on purpose: UnitTests may reference only Domain, and IntegrationTests must not reference the UnitTests project). |
| `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfilePictureEndpointsTests.cs` | Picture API behaviour tests (real LocalDB via `IdentityApiFactory`). |

Modify (tests):

| File | Change |
| --- | --- |
| `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileRouteSecurityTests.cs` | Picture routes added to the 401 and 403 matrices; grant/order cases. |
| `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileErrorTests.cs` | Picture mappings with wrapped transient inner exceptions; `FailingApplication` signature; header assertions on 500/503. |
| `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileEndpointsTests.cs` | OpenAPI inventory now includes both picture ops (`excludeProposed` removed); GET/PUT `/me` return `picture: null`. |
| `tests/QuizArena.Identity.IntegrationTests/Application/ProfileApplicationTests.cs` | Valid sample bytes; new return type; `GetInfoAsync` setups for strict mocks; marker tests replaced (no markers anywhere; picture required + nullable); serialization with null and value. |

Delete: none. Not touched: `QuizArena.Identity.Data`, migrations, client (`src/QuizArena.Client`, owned by #145/#151),
other services, `specs/implementation-plan.md`.

## 3. Ordered steps

**Step 1 - Domain format validator.** File: `ProfilePictureFormat.cs`.
`Validate(string contentType, ReadOnlySpan<byte> content)`; all failures throw `InvalidProfileException("content", msg)`
with short generic messages (no byte dumps):
- empty → "Picture content is required."; `contentType` not in `ProfilePicture.AllowedContentTypes` (exact, lowercase)
  → `InvalidProfileException("contentType", ...)` (the API passes a canonical type, so this is a guard).
- detect format by magic: PNG `89 50 4E 47 0D 0A 1A 0A`; JPEG `FF D8 FF`; WebP `RIFF....WEBP`. Unknown magic →
  "Picture content is not a PNG, JPEG or WebP image."; detected ≠ declared → "Picture content does not match the Content-Type."
- PNG: length ≥ 33; bytes 8-11 = big-endian 13; bytes 12-15 = `IHDR`. No CRC, no width/height check.
- JPEG: walk markers from offset 2: skip `FF` fill bytes; standalone markers `D0-D7`/`01` have no length; otherwise
  read 2-byte big-endian segment length (≥ 2, in bounds). Success on SOFn (`C0-C3, C5-C7, C9-CB, CD-CF`) with length ≥ 8
  and in bounds. Fail on SOS (`DA`) or EOI (`D9`) before a SOF, on a non-`FF` marker byte, or when out of bounds.
- WebP: length ≥ 12; RIFF size (LE uint32 at 4) + 8 ≤ length; bytes 8-11 `WEBP`; first chunk at 12 needs an 8-byte
  header in bounds and chunk size ≤ remaining; fourCC `VP8 ` (payload ≥ 10 and bytes 3-5 of payload `9D 01 2A`),
  `VP8L` (payload ≥ 5 and first byte `0x2F`) or `VP8X` (payload ≥ 10); anything else fails.
Done when: Step 1 unit tests pass and `ArchitectureTests` still pass (Domain has no NuGet).

**Step 2 - DTOs.** `ProfilePictureResponse(ContentType, UpdatedAt, Version)` + `From(ProfilePictureInfo)`;
`ProfileResponse(Guid Id, string Issuer, string Subject, string DisplayName, ProfilePictureResponse? Picture)` and
`From(PlayerProfile profile, ProfilePictureInfo? picture)`. Web JSON names: `picture`, `contentType`, `updatedAt`, `version`;
null is serialised as `"picture": null` (default ignore condition is Never). Done when: build is green.

**Step 3 - Application.** `IProfileApplication.ReplaceMyPictureAsync(...)` → `Task<ProfileResponse>`.
`ProfileApplication.ReplaceMyPictureAsync`: `EnsurePlayer` → `ProfilePictureFormat.Validate(contentType, content)` →
`FindByIdentityAsync` (null → `PlayerProfileNotFoundException`) → `pictures.ReplaceAsync(profile.Id, ...)` →
`ProfileResponse.From(profile, info)`. `GetMeAsync` and `UpsertMeAsync` call `pictures.GetInfoAsync(profile.Id, ct)` after
the profile is known and pass it to `From`. `GetMyPictureAsync` unchanged. Done when: `ProfileApplicationTests` pass.

**Step 4 - Exception mapping.** `IdentityExceptionHandler.TryHandleAsync`, after the cancellation branch and guarded by
`!httpContext.Response.HasStarted`, in this order:
`ProfilePictureCorruptException` → 500 `{title "Unexpected failure"}` (no detail, no inner text);
`ProfilePictureCommitOutcomeUnknownException` → 500 `{title "Upload outcome unknown", detail "The upload could not be confirmed. Reload your profile before retrying."}`;
`ProfilePictureConflictException` → 409 `{title "Conflict", detail "The profile picture was changed by another request. Retry the upload."}`;
`PlayerProfileNotFoundException` → 404 `{title "Profile not found", detail "Save a display name to create your profile."}`;
then the existing `InvalidProfileException` → 400 and the 503 walk. `IsDependencyUnavailable` returns `false` as soon as
it meets any `IdentityDomainException` in the chain. Use a small private helper to write a `ProblemDetails` so the five
branches stay short. Done when: `ProfileErrorTests` pass.

**Step 5 - Request plumbing.** `RequireImageContentTypeAttribute`: `TryGetImageMediaType(header, out mediaType)` parses
with `MediaTypeHeaderValue.TryParse`, ignores parameters, compares case-insensitively to `image/png|image/jpeg|image/webp`
and outputs the canonical lowercase value. `OnResourceExecuting`: missing/unparsable/other (incl. `image/jpg`,
`image/svg+xml`, `image/gif`, `application/octet-stream`, `multipart/form-data`) → `UnsupportedMediaTypeResult` (same
problem+json path as the JSON filter). `SetContentTypes` lists the three image types. No `[Consumes]`.
`PictureRequestBody.ReadAsync(request, limit, ct)`: `ContentLength > limit` → null without reading; else read into a
buffer of size `min(ContentLength ?? limit + 1, limit + 1)` (grow-on-demand `MemoryStream` capped at `limit + 1` for
chunked), stop once `limit + 1` bytes are seen → null; otherwise return the exact bytes.
`PrivatePictureResponseAttribute` + `UsePrivatePictureHeaders()`: middleware reads
`context.GetEndpoint()?.Metadata.GetMetadata<PrivatePictureResponseAttribute>()`; when present registers
`Response.OnStarting` that **assigns** (not appends) `Cache-Control = "private, no-store"` and
`X-Content-Type-Options = "nosniff"`. Registered after `UseExceptionHandler` so it also covers 500/503 (OnStarting
callbacks run LIFO, so ours overrides the exception handler's cache headers). Done when: Step 7 header tests pass.

**Step 6 - Controller actions** (`ProfilesController`, both decorated with `[PrivatePictureResponse]`):
- `[HttpGet("me/picture")] [Authorize(Policy = IdentityPolicies.ProfilesRead)]`
  `[ProducesResponseType(typeof(byte[]), 200, "image/png", "image/jpeg", "image/webp")]` + 404 problem; no `[Produces]`.
  `GetMyPictureAsync` → null → `Problem(404, "Picture not found", "No profile or no picture for the caller.")`;
  else `File(picture.Content, picture.ContentType)`.
- `[HttpPut("me/picture")] [Authorize(Policy = IdentityPolicies.ProfilesWrite)] [RequireImageContentType]`
  + `ProducesResponseType<ProfileResponse>(200)` and problem types for 400/404/409/413/415. No `[FromBody]` parameter.
  Body: `TryGetImageMediaType` (canonical type) → `PictureRequestBody.ReadAsync(Request, ProfilePicture.MaxContentLength, ct)`
  → null → `Problem(413, "Payload too large", "Image exceeds 2,097,152 bytes.")` → `Ok(await profiles.ReplaceMyPictureAsync(actor, type, bytes, ct))`.
  400/404/409/500/503 come from the exception handler. Done when: picture endpoint tests pass and the OpenAPI
  inventory test lists both picture operations.

**Step 7 - Contract.** `contracts/openapi/identity.yaml`: delete the six marker keys; rewrite descriptions without
"Proposed" (e.g. "Implemented by #144 (SEC-01, SEC-08): ..."); info description: "Version 1 operations are implemented
by the Identity service. See docs/milestone3/api-contracts.md. ..."; `Profile.required` += `picture`; picture
description "metadata of the caller's picture, or null when none (always present). Never contains a storage key, path or
URL."; add `"picture": null` to the `/me` GET and PUT 200 examples. Statuses, headers and image media stay unchanged.
Done when: `node contracts/validate-contracts.mjs` passes after Step 8.

**Step 8 - Validator.** `contracts/validate-contracts.mjs`: `const proposedOps = {};` (comment: no operation is proposed
now; a future one is listed here with its issue); line ~242 → `fail(operations===32,'Expected 32 operations')`.
Probes:
- line 272 → `['implemented picture operation marked proposed', d=>{Object.assign(d.identity.paths['/api/users/me/picture'].put,{'x-status':'proposed','x-implemented-by':'#144'});}, /Proposed marker mismatch/]`
- line 273 → `['implemented-by without status', d=>{d.identity.paths['/api/users/me/picture'].put['x-implemented-by']='#144';}, /x-implemented-by without x-status/]`
- line 281 → `['required property marked proposed', d=>{Object.assign(d.identity.components.schemas.Profile.properties.picture,{'x-status':'proposed','x-implemented-by':'#144'});}, /Proposed property must not be required/]`
- add `['profile example without picture', d=>{delete d.identity.paths['/api/users/me'].get.responses[200].content['application/json'].example.picture;}, /missing picture/]`.
Done when: the validator prints PASS with 32 operations and every probe throws with its expected reason.

**Step 9 - Docs.** Update the four docs listed in the inventory. Wording rules: describe the routes as implemented by
#144; keep "team approval pending" for SEC decisions; SEC-03 gets "Implementation decision (#144, not an approval):
`picture` is required and nullable"; SEC-06 gets "Implementation note (#144, not an approval): an unknown commit outcome
returns a safe 500 asking the client to reload its profile before retrying; Conflict → 409, missing profile → 404,
corrupt stored row → safe 500; transient inner causes no longer turn these into 503." Threat-model rows 47-49 cite
`ProfilePictureFormatTests`, `ProfilePictureEndpointsTests`, `ProfileErrorTests`. Done when: `grep -n -i proposed` over
these docs and identity.yaml shows no claim that the picture routes are unimplemented.

**Step 10 - Tests.** Add and update the tests in Section 5. Done when all validation commands in Section 6 pass.

## 4. Acceptance coverage

| # | Criterion (issue #144) | Steps | Evidence (tests / checks) |
| --- | --- | --- | --- |
| 1 | Implement the agreed operations through `ProfilesController` and the profile application layer | 2, 3, 5, 6, 7, 8 | `ProfilePictureEndpointsTests` happy paths; `OpenApiDocument_InDevelopment_ListsExactlyTheContractOperations` incl. picture ops; validator PASS (32 ops, no markers); `ProfileApplicationTests` |
| 2 | Authorize each upload and image read from the validated caller | 3, 5, 6 | `ProfileRouteSecurityTests` picture × 9 unauthenticated kinds (401) and × 9 forbidden kinds (403); grant/order cases; owner resolved only from token (application tests) |
| 3 | Valid upload/replacement and authorized response with expected bytes and content type | 3, 6 | PNG/JPEG/WebP upload → 200 Profile with picture v1; replace → v2 and GET returns new bytes byte-for-byte with stored `Content-Type` and both headers |
| 4 | Unit-test domain rules; API-test allowed files, exact limit and +1, unsupported formats, misleading types, malformed images | 1, 5, 6 | `ProfilePictureFormatTests`; API: exact 2,097,152 → 200; +1 → 413 (Content-Length and chunked); 415 set; mismatch → 400; truncated/malformed → 400 |
| 5 | Documented statuses; each rejected upload preserves saved profile and readable picture | 4, 5, 6, 7 | Each rejection (400/404/409/413/415/401/403/503) followed by GET `/me` + GET picture showing the prior name, version and bytes; statuses equal contract outcomes |
| 6 | Unauthenticated, insufficient-scope and different-account reads/replacements fail; changed request IDs/references ignored | 3, 5, 6 | Route security matrix; player B cannot read/replace A's picture (B gets own 404, A unchanged); spoofed query (`?userId=`, `?profileId=`), headers (`X-User-Id`) and a `/api/users/{id}/picture`-style path do not select another owner |
| 7 | Safe dependency errors and unchanged profile state | 4, 5 | `ProfileErrorTests` wrapped-transient mappings (409/404/500/500/503) with no sentinel/"Exception" text and both headers; real-DB transient failure on replace → 503 and previous picture still served |

(The Dispatcher brief mentions six criteria; the issue lists seven checkboxes. All seven are mapped.)

## 5. Testing strategy

Conventions: xUnit, FluentAssertions, Moq; Arrange/Act/Assert comments; `Method_Scenario_Expected` names; existing
projects and folders only. API tests use `IdentityApiFactory` (real LocalDB, Development) and `ByteArrayContent` /
`StreamContent` with explicit `Content-Type`. Each test uses a fresh subject (`NewSubject()`).

**Unit - `tests/QuizArena.Identity.UnitTests/Domain/ProfilePictureFormatTests.cs`**
- `Validate_WithMinimalValidImage_DoesNotThrow` (theory: PNG, JPEG baseline SOF0, JPEG progressive SOF2 after APP0/DQT,
  WebP VP8, VP8L, VP8X).
- `Validate_WithEmptyContent_ThrowsContentRequired`.
- `Validate_WithUnknownMagic_Throws` (GIF89a, SVG text, BMP, random bytes).
- `Validate_WithMismatchedContentType_Throws` (PNG bytes as jpeg/webp, JPEG as png, WebP as png).
- `Validate_WithTruncatedPng_Throws` (signature only, 32 bytes, IHDR length ≠ 13, chunk type ≠ IHDR).
- `Validate_WithMalformedJpeg_Throws` (SOI only, SOS before SOF, EOI before SOF, segment length past end, length < 2,
  SOF length < 8, non-FF marker byte, DHT `C4`/`C8`/`CC` not treated as SOF).
- `Validate_WithMalformedWebp_Throws` (RIFF size past end, not `WEBP`, unknown first chunk, chunk size past end,
  VP8 missing `9D 01 2A`, VP8L wrong signature byte, VP8X payload < 10).
- `Validate_WithUnsupportedContentType_Throws` (`image/gif`, `image/JPEG` - Domain is exact).
- `Validate_FailureMessage_DoesNotEchoContent`.

**API - `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfilePictureEndpointsTests.cs`**
- `PutPicture_WithValidImage_Returns200ProfileWithPictureVersion1` (theory png/jpeg/webp); then
  `GetPicture_AfterUpload_ReturnsExactBytesContentTypeAndPrivateHeaders`.
- `PutPicture_Replace_IncrementsVersionAndGetReturnsNewBytes` (PNG → WebP; content type changes too).
- `PutPicture_WithContentTypeCaseAndParameters_IsAccepted` (`IMAGE/PNG; charset=x`) and stores canonical `image/png`.
- `GetMe_AfterUpload_IncludesPictureInfo`; `GetMe_WithoutPicture_ReturnsPictureNull`.
- `GetPicture_WithoutProfile_Returns404`; `GetPicture_WithProfileButNoPicture_Returns404WithPrivateHeaders`.
- `PutPicture_WithoutProfile_Returns404AndCreatesNothing` (valid bytes; no profile row, no picture row afterwards).
- `PutPicture_AtExactSizeLimit_Returns200` (valid PNG padded with a trailing ancillary chunk/bytes to 2,097,152).
- `PutPicture_OneByteOverLimit_Returns413` (theory: with Content-Length; chunked `StreamContent` without length).
- `PutPicture_WithDeclaredContentLengthOverLimit_Returns413WithoutReadingBody` (stream that throws if read past header).
- `PutPicture_WithUnsupportedMediaType_Returns415` (missing, `image/jpg`, `image/svg+xml`, `image/gif`,
  `application/octet-stream`, `multipart/form-data`, `text/plain`).
- `PutPicture_WithMisleadingContentType_Returns400` (PNG bytes as image/jpeg, SVG text as image/png, GIF as image/webp).
- `PutPicture_WithMalformedImage_Returns400` (empty body, truncated PNG/JPEG/WebP).
- Order: `PutPicture_UnsupportedTypeAndOversized_Returns415` (415 before 413);
  `PutPicture_OversizedAndMalformed_Returns413`; `PutPicture_MalformedWithoutProfile_Returns400` (400 before 404).
- `PutPicture_EachRejection_PreservesProfileAndPicture` (theory over 400, 413 and 415 bodies plus 401/403 tokens):
  upload v1, send the rejected request, assert GET `/me` name and `picture.version == 1` and GET picture bytes equal v1.
- No API-level concurrency race test: the store race is covered by
  #143 store tests and the 409 mapping by `ProfileErrorTests`.
- `PictureRoutes_DifferentAccount_CannotReadOrReplaceOthersPicture`: A uploads; B GET → 404 (own), B PUT with no
  profile → 404; A's bytes/version unchanged.
- `PictureRoutes_WithSpoofedOwnerReferences_UseOnlyTokenIdentity`: B sends `?userId=<A id>&profileId=<A id>`,
  `X-User-Id`/`X-Profile-Id` headers; B's own state only; A unchanged. `/api/users/{A id}/picture` → 404/405 and A unchanged.
- `PictureRoutes_RejectionResponses_CarryPrivateHeaders` (401, 403, 404, 413, 415, 400).

**Security matrix - `ProfileRouteSecurityTests.cs`**: change the case data to `(string route, string kind)` over
`GET /me`, `PUT /me`, `GET /me/picture`, `PUT /me/picture` × the existing nine 401 kinds and nine 403 kinds; picture
PUT sends a valid PNG body; after each request assert no profile and no picture was written (`ShouldHaveNoPicture`
helper via a fresh `IdentityDbContext` scope). Grant/order cases: GET picture with only `profiles.write` → 403; PUT
picture with only `profiles.read` → 403; machine token → 403; read-only token + `image/svg+xml` → 403 (not 415);
read-only token + 3 MB body → 403 (not 413); no token + 3 MB body → 401.

**Errors - `ProfileErrorTests.cs`**: `FailingApplication.ReplaceMyPictureAsync` returns `Task<ProfileResponse>`. Theory
over PUT picture with `Conflict(SqlFailure(-2))` → 409, `NotFound(SqlFailure(40613))` → 404,
`Corrupt(new InvalidProfileException(...Sentinel...))` → 500, `OutcomeUnknown(SqlFailure(-2))` → 500 with "reload"
detail, `TimeoutException`/`SqlFailure(-2)` → 503; GET picture with `SqlFailure(40613)` → 503 and Corrupt → 500. All
via `AssertSafeProblemAsync` (no sentinel, no "Exception") plus both private headers. Real-DB case:
`PutPicture_WhenStoreFailsTransiently_Returns503AndKeepsPreviousPicture` uses `WithWebHostBuilder` +
`ConfigureTestServices` to decorate the real `IProfilePictureStore` (reads delegate; `ReplaceAsync` throws
`SqlFailure(-2)`), after uploading v1 through the normal factory; then GET picture returns v1.

**Application/contract - `ProfileApplicationTests.cs`**: replace the 4-byte array with `PictureSamples.Png`; assert
`ProfileResponse` (with `Picture`) instead of `ProfilePictureInfo`; add `ReplaceMyPictureAsync_WithInvalidFormat_ThrowsBeforeLookup`
(strict mocks prove no repository/store call); `GetMeAsync_WithPicture_IncludesInfo`; strict `GetInfoAsync` setups in
GetMe/Upsert tests. Replace the two marker tests with `ContractDocument_HasNoProposedMarkers` (operations and schema
properties) and `ProfileContract_Picture_IsRequiredNullableWithoutStorageFields`; serialization test for `Picture = null`
(property present, value null) and for a value (`contentType`, `updatedAt`, `version` only).
**`ProfileEndpointsTests.cs`**: drop `excludeProposed`; existing GET/PUT `/me` tests assert `picture` is null.

## 6. Validation commands

Run from the worktree root (`C:/Web-3-Final-Project-wt/144`). Requires .NET SDK per `global.json`, SQL Server LocalDB
for Identity integration tests, and Node.js for the validator. Report the counts actually printed; do not reuse
historical counts (Prime baseline for the validator: 32 operations, 201 examples before the change).

```
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test tests/QuizArena.Identity.UnitTests --no-build
dotnet test tests/QuizArena.Identity.IntegrationTests --no-build
dotnet test tests/QuizArena.ArchitectureTests --no-build
node contracts/validate-contracts.mjs
```

Static check: `grep -n -i "x-status\|proposed" contracts/openapi/identity.yaml` returns nothing.

## 7. Risks and out of scope

Risks and mitigations:
- **Contract change for clients**: `picture` becomes required (nullable). The React client (#151/#145) already treats
  absent and null alike; other consumers of `/me` do not exist. Called out in the PR body.
- **GET `/me` now reads picture metadata**: a corrupt stored picture row makes `/me` a safe 500 (#143 decision that
  corrupt rows fail loudly). Accepted; documented in SEC-06 note.
- **OnStarting header override**: relies on LIFO callback order relative to `ExceptionHandlerMiddleware`'s cache-header
  reset. Covered by explicit 500/503 header assertions; if it fails, set headers inside `IdentityExceptionHandler` for
  marked endpoints as well.
- **OpenAPI generation** may not emit the raw image `requestBody`; the inventory test compares operations only. An
  OpenAPI transformer is optional and not required by the acceptance criteria.
- **TestServer vs Kestrel**: Kestrel's default 30 MB body limit is above our cap, so our 413 is always reached first.
- **Existing tests with dummy bytes**: format validation in the application layer breaks them; Step 10 replaces them
  with valid samples. `ProfilePicture` entity tests and store tests are untouched (entity does not validate format).
- Approval: SEC-01..SEC-08 remain "team approval pending"; SEC-03/SEC-06 edits are labelled #144 implementation
  decisions/notes, not approvals.

Out of scope (residual risks, recorded in the PR body and threat model):
- Pixel-dimension limits and decompression bombs (no decoding or dimension parsing).
- EXIF/metadata stripping and re-encoding.
- Slow-upload / slowloris protection beyond Kestrel defaults; rate limiting.
- Picture delete endpoint, ETag/If-Match, push sync between devices (SEC-07/08/09 later work).
- Client upload/rendering (#145) and the cross-device journey automation (#147).
- Any change to `QuizArena.Identity.Data`, migrations or other services.

Blockers: none.
