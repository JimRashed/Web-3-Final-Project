# #147 (SEC-D2) — automate the cross-device profile-picture journey

> Issue: #147 · Parent: #136 (SEC-D) · Tracker: #132 · Owner: @SuSu-Lisu
> Branch: `feat/147-cross-device-journey`, base `feature/milestone-4-security-and-cross-device-profiles`.

## What "cross-device" has to mean

The same account, signed in twice, in two sessions that share no token and no browser context. Device A
uploads; device B retrieves the *service's* copy, not a copy the browser cached; a third account is
refused; a replacement is visible to B; and a rejected upload leaves the previous picture intact.

Sign-out cannot be proven at API level — a Keycloak access token stays valid until it expires — so the
client half of that criterion lives in the component test added in #151 (sign-out unmounts the panel and
revokes the object URL). This script proves the service half.

## How it runs

`tests/e2e/picture-journey.mjs`, wired as `npm run journey:picture`, following the shape of the existing
`journey:api`/`journey:retry` scripts: it reads `.env` (never prints secrets), signs both sessions in
through the real Keycloak screen, then talks to Identity over HTTP.

The PNG fixtures are generated in the script (CRC32 + zlib deflate) so they are valid by construction
rather than hand-copied hex, and two different colours give two different byte sequences.

## Checks

| Check | Why |
|---|---|
| two sessions for one account hold different tokens | otherwise "cross-device" proves nothing |
| A uploads, B reads the same bytes (SHA-256) | the store is shared, not per-session |
| `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff` | the bytes are never stored by a shared cache |
| a third account cannot read A's picture | ownership is derived from the token, not the request |
| a third account's upload does not change A's picture | the same |
| replacement increments the version and B sees it | atomic replace, single current picture |
| unauthenticated read is 401 | the route is private |
| text/plain, a mismatched magic number and 2 MiB + 1 are refused | the validation the contract promises |
| a rejected upload keeps the previous picture | failure does not destroy state |

## Status

Written against the approved contract (A1, #137); it cannot pass until C2 (#144 / PR #159) serves the
two operations. Run it with the stack from the runbook:

```powershell
./scripts/start-milestone3.ps1
cd tests/e2e; npm ci; npm run journey:picture
```
