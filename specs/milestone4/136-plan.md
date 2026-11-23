# #136 (SEC-D) — verify service contracts and the cross-device journey

> Issue: #136 · Children: #146 (D1), #147 (D2), #148 (D3, @JimRashed) · Tracker: #132 · Owner: @SuSu-Lisu

This is a roll-up: it closes when its three children are accepted and their evidence is in the
milestone PR. Nothing here is new production code.

## Child status

| Child | What it delivers | State |
|---|---|---|
| **#146** D1 | Gameplay's machine-facing contract verification: route, verb, actor, scope, audience and body for the four receivers, plus the failure mapping | **done** — #157 merged (`a35d4f6`), issue closed |
| **#147** D2 | The cross-device picture journey: two sessions for one account, upload on A, read on B, third account refused, replacement, private headers, validation refusals | **drafted** — #160 open, cannot pass until C2 (#144 / #159) serves the operations |
| **#148** D3 | Integration of the verification evidence into the milestone PR | @JimRashed, open |

## What "accepted" means for D2

The script (`tests/e2e/picture-journey.mjs`, `npm run journey:picture`) has to be **run against a live
stack** and its PASS lines pasted into #147. The component-level half of the same criterion — sign-out
clears the picture and revokes its bytes — is covered in #151, because a Keycloak access token outlives
a sign-out and an API script cannot prove a client-side state change.

## Definition of done for #136

1. #146 and #147 accepted with their evidence linked.
2. #148 has the results in the milestone PR description.
3. The two claims this feature makes are true and linked: *contracts are verified, not assumed* (D1) and
   *the same picture is reachable from a second device and only from that account* (D2).

## Dependencies

- #144 (C2) blocks D2's execution; #145 (C3) blocks its UI-level half.
- No dependency on D1's outcome; the two children are independent by design.
