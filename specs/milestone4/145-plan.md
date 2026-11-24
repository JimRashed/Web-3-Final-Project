# #145 (SEC-C3) — picture upload and rendering in the React profile

> Issue: #145 · Parent: #135 (SEC-C) · Tracker: #132 · Owner: @SuSu-Lisu
> Branch: `feat/145-profile-picture-ui`, base `feature/milestone-4-security-and-cross-device-profiles`.

## Scope of this slice

The client half of the private profile picture. The server half is #144; this PR does **not** need it to
land, because the approved contract (A1, #137) already fixes the shape:

- `GET /api/users/me/picture` — the caller's own bytes (`image/png`, `image/jpeg` or `image/webp`),
  `profiles.read`, owner derived only from the validated issuer/subject, `Cache-Control: private, no-store`.
  `404` when there is no picture.
- `PUT /api/users/me/picture` — raw body, **not multipart**, at most **2,097,152 bytes**, the magic bytes
  must match the declared content type, returns the updated `Profile`.
- `Profile.picture` — metadata only (`contentType`, `updatedAt`, `version`), never a storage key or URL.

## Design

**Nothing cacheable or shareable is left on the device.** The bytes are fetched with the bearer token
through the normal client (`responseType: 'blob'`), turned into an object URL that lives in a ref, and
revoked when the picture is replaced and when the component unmounts — which is exactly what a sign-out
does, because `SignedInOnly` drops the subtree.

**Validate locally, then again on the server.** `pictureFileError` rejects a wrong type, an empty file or
anything over 2 MiB before a request is made, so the player gets the reason immediately; the server
repeats every rule and stays authoritative.

## Files

| File | Change |
|---|---|
| `src/QuizArena.Client/src/api/httpClient.ts` | `responseType: 'blob'` responses and raw `Blob` request bodies with their own media type. |
| `src/QuizArena.Client/src/api/identity.ts` | `pictureTypes`, `maxPictureBytes`, `pictureFileError`, `PictureMetadata`, `Profile.picture`, `picture()`, `replacePicture()`, and contract validation of the picture metadata. |
| `src/QuizArena.Client/src/features/profile/ProfilePage.tsx` | `ProfilePicture` panel: current image, empty state, upload/replace, permission and retry states, object-URL lifecycle. |
| `src/QuizArena.Client/src/features/profile/Profile.css` | Panel and thumbnail styles. |
| `src/QuizArena.Client/src/features/profile/ProfilePage.test.tsx` | Display, empty, wrong type, oversize, upload, 403 and revocation. |

## Validation

`npm run typecheck` and `eslint --max-warnings 0` are clean locally. Vitest cannot run in the agent
sandbox (it spawns a child process with piped stdio, which the sandbox blocks), so the test evidence is
CI on this PR.

## Not in this slice

- The server operations and their status codes (#144).
- The cross-device journey (#147), which needs #144 and this PR together.
- Acceptance of #145 still requires C2 (#144): the UI is prepared, not integrated.
