# Issue #48: Lobby React page and public REST integration

## Summary and architecture

Issue [#48](https://github.com/JimRashed/Web-3-Final-Project/issues/48), S04-T05, belongs to parent [#12](https://github.com/JimRashed/Web-3-Final-Project/issues/12) and the single partial draft PR authorized by the user's later steering. Work remains on `feature/m3/12-lobby-service`, based on `4f5d8cb`. #44–47 passed their separate stage loops on this branch; #22/#17 are complete. This is an agent-reviewed implementation plan with final human review pending.

**Eligible implementation: PASS. Live completion: BLOCKED by external [#53](https://github.com/JimRashed/Web-3-Final-Project/issues/53).** The authenticated dependency record says #53 remains open, Backlog, blocked by #52 with no linked development PR. Implement the public Lobby UI and contract-based Gameplay start request using existing fetch doubles. Do not claim real Gameplay creation/navigation or full guest entry has passed. External #24 remains open with unmerged draft PR #83; #49 implementation stays unstarted.

Reuse the existing session, configured service clients, safe ApiError/RequestState and accessible ValidatedField. Public Lobby flags/counts suffice: no new subject plumbing, host/member names, machine scopes or internal calls. Keep all backend and shared session/client behavior unchanged. Mount the real feature at the existing `/lobby` destination and navigate a successful host start to the documented `/matches?matchId=...` path using only the returned ID; its destination remains a clearly incomplete shell placeholder, not a Gameplay implementation.

## Exact file inventory

Repair #48-T1 changes the new destination to a query parameter because contract-valid opaque IDs `.` and `..` are normalized when used as URL path segments, even when dot-encoded. New navigation and copy links use `/matches?matchId=...` and round-trip the exact ID through URLSearchParams. The existing `/matches/:matchId` shell remains compatible for earlier links. This is a route representation correction in the same planned files, with no Gameplay schema narrowing or peer implementation.

Repair #48-V1 aligns returned MatchId validation exactly with Gameplay's nonblank string and 100-Unicode-code-point limit. Preserve escaped control characters and non-BMP characters; do not impose a character allowlist or count UTF-16 code units. Tests include escaped controls, exact 100/101 code-point boundaries, 51 emoji, malformed types and exact navigation/copy roundtrip.

Create:

- `src/QuizArena.Client/src/api/lobby.ts`: public six-field LobbyResponse type, strict code helper, create/get/join/bodyless-lock wrappers and a small Gameplay start wrapper using its separate configured public client with `{joinCode}` only. Minimal response validation rejects missing/malformed data safely; no backend DTO/internal fields are introduced.
- `src/QuizArena.Client/src/api/lobby.test.ts`: fetch-double URL/body/auth/cancellation/error/response tests for both service origins.
- `src/QuizArena.Client/src/features/lobby/LobbyPage.tsx`: session-gated create/join forms, selected lobby, code copy, membership/count/state, host controls, sequential polling, mutation coordination and safe errors.
- `src/QuizArena.Client/src/features/lobby/LobbyPage.css`: feature-local responsive layout/focus/state styles using existing shell conventions.
- `src/QuizArena.Client/src/features/lobby/LobbyPage.test.tsx`: accessible workflow, controls, clipboard, statuses and host/guest behavior.
- `src/QuizArena.Client/src/features/lobby/LobbyPage.lifecycle.test.tsx`: deterministic polling, stale reads, mutation duplication and teardown races.

Modify:

- `src/QuizArena.Client/src/app/App.tsx`: mount LobbyPage for `/lobby`, retain other destinations, add only the returned `/matches?matchId=...` shell destination and `/notifications` alias to the existing Events placeholder when needed by the documented guest waiting link. The selected-match shell may display the returned ID and copyable same-origin link, but must not show fabricated match data or claim participant access.
- `src/QuizArena.Client/src/app/App.test.tsx`: adapt Lobby placeholder assertions and add actual Lobby mount/returned-match navigation checks; preserve other shell/explicit-sign-in/session assertions.
- `docs/milestone3/frontend-setup.md`: describe implemented Lobby configuration/flows, exact current checks and explicitly pending live Gameplay/guest acceptance, without changing historical foundation evidence into new results.

No package/lockfile, backend, shared session/client, Gameplay/Notification screen or unrelated visual edits. Keep lifecycle coordination in the feature component unless a cohesive feature-local helper becomes necessary; record any inventory adjustment in this scoped plan first. No new mocking framework or dependencies.

## Ordered steps

1. **Define the bounded public feature client.** Accept existing configured clients (`createClients(readConfig(import.meta.env), session)`), with a stable memoized instance per session. Lobby DTO has exactly `lobbyId`, `joinCode`, `status: 'Open' | 'Locked'`, `memberCount`, `isMember`, `isHost`; consume no host/player IDs or persistence data. Provide create POST `/api/lobbies` `{}`, get GET `/api/lobbies/{code}`, join POST `/api/lobbies/{code}/join` `{}` and lock POST `/api/lobbies/{code}/lock` without a body. Validate user codes with `^[A-Z0-9]{6}$`, rejecting lowercase/whitespace rather than silently changing them. Pass AbortSignal on every call. Use the existing HTTP client for current bearer token, configured origin, no cookies/redirects, public-path protections, safe errors and no retries. Add `startMatch` using **Gameplay** client POST `/matches` and body exactly `{joinCode}`; accept normal JSON success from 200 or 201, take only a valid nonblank returned matchId (contract max 100), and URL-encode that ID when navigating. Never guess an ID or use lobbyId/code as matchId. Malformed/missing success DTOs fail safely; no raw upstream content is displayed. Completion: fetch-double tests prove correct origins, methods, bodies, signals and use of returned matchId for both success statuses.
2. **Build the accessible page.** Use useSession: loading shows status, anonymous/expired shows explicit sign-in and unavailable shows safe unavailable guidance; no automatic login. Authenticated empty state offers Create lobby and a labeled Join code form. Use ValidatedField for required exact-six-uppercase validation, visible help/error and aria associations, and focus the invalid field on submit. Successful create/join selects the returned lobby. Show code as selectable text with Copy code, state, member count out of two, caller-relative host/member labels and waiting guidance; do not invent member names. Use busy/status announcements and disabled submission while pending. Clipboard success is announced; absent/denied clipboard leaves a selectable code and clear manual-copy guidance, never false success. Completion: keyboard-accessible form/control tests cover validation, empty/loading, successful create/join, copy and fallback.
3. **Gate lock/start and coordinate mutations.** Show host lock enabled only for Open with two members; host start enabled only for Locked with two. Nonhost lock/start are disabled with visible host-only explanation; host waiting for a second member also has an explanation. Backend remains authoritative. Use an immediate mutable in-flight guard in addition to rendered disabled state so rapid repeated events cannot submit twice. Lock calls only Lobby lock; Start calls only public Gameplay create after Locked has been observed. Neither mutation retries automatically. On success use the actual returned Lobby/Match response; navigate to `/matches?matchId=${encodeURIComponent(matchId)}` and provide a copyable same-origin link from the returned destination. The shell shows no scores/questions/participant information. Guest waiting links to the documented notifications destination via the minimal existing Events alias and explains that delivery can be delayed; do not infer an event/match correlation, poll internal APIs or implement the unavailable peer screens. Completion: host/guest/state tests prove gating, no premature Gameplay call, one request per action and correct returned-ID navigation/link.
4. **Implement sequential polling and race protection.** For the selected code while authenticated, use modest two-second foreground refresh with one request at a time; schedule the next timeout only after the previous read settles. Cancel timeout/current read when changing selected lobby, beginning a mutation, leaving the page or losing/replacing the session. Increment a request/lobby generation and ignore late results from canceled/previous generations even if a test fetch ignores AbortSignal. A preview started before lock must never overwrite the successful Locked mutation response with old Open data. A new create/join must invalidate reads of the previous lobby. Resume current-lobby polling after a successful mutation using the new generation; keep requests stable across ordinary renders by memoizing the client/config. Pause background polling when the document is hidden if using visibility handling; remove listeners on teardown. Completion: controlled deferred promises/fake timers prove non-overlap, cleanup, no obsolete-state restoration and no request churn across renders.
5. **Handle errors with existing safe states.** Local input errors stay attached to the code field; use the shared ApiError message/RequestState for backend failures, never raw ProblemDetails/provider/SQL content. 400 explains invalid input with safe local validation; 401 clears selected lobby/code/private response state via session invalidation, aborts work and offers only explicit sign-in. 403 preserves session and shows permission guidance without a retry loop. 404 explains the missing lobby and offers new code/create selection. 409 explains changed/full/Locked state and performs at most one current-lobby refresh to show authoritative state, without repeating the failed mutation. 503/network failure offers an explicit bounded user retry action; no repeated mutation or failing poll loop. Generic failures remain safe. Abort/stale responses produce no error banner or late navigation. Completion: status table tests assert the correct session/state/action, request counts and absence of automatic login/mutation retries.
6. **Mount, document and verify.** Replace only Lobby's destination placeholder; retain all unrelated routes. Add the minimal documented selected-match path/notification alias required by the feature links. Do not implement Gameplay or Notification screens or pretend direct navigation establishes participation. Update setup documentation to distinguish code/tests from not-yet-run live acceptance. Run focused and full frontend gates and root-owned aggregate regression; hand actual results to Testing/Validate/read-only Code Review. Completion: no new dependency/backend changes, existing shell tests preserved and live blockers explicitly recorded.

## Acceptance coverage

| Criterion | Steps and evidence |
|---|---|
| LobbyPage create/join forms and REST module | 1–2, 6; exact public requests, successful selection and route mounting |
| Join code display/copy | 2; visible/selectable code, clipboard announcement and denied/unavailable fallback |
| Host/player membership and lock state | 2–3; safe count/flags labels, Open/Locked state, no subject/name leakage |
| Host-only lock/start | 3; Open-two lock, Locked-two start, nonhost disabled explanations and backend error handling |
| Polling and cleanup | 4; sequential refresh, signals/timers, stale-read generation, unmount/session teardown tests |
| Start calls Gameplay create after lock, navigation to returned match | 1, 3, 6; distinct configured Gameplay origin, `{joinCode}` only, 200/201 and encoded returned-ID route |
| Loading/empty/field/submit and 400/401/403/404/409/503 states | 2–5; accessible state table, explicit recovery, session preservation/clearing and no retry loop |
| Public data/session use, no internal secrets/backend dependency | 1–6; flags-only DTO/client tests, shared bearer/session and unchanged packages/backend |
| Typecheck/lint/component tests/build | 6; actual commands/results recorded by runtime stages |
| Live owned API and Gameplay acceptance before closure | **Pending**: Lobby #47 exists on branch; external #53 remains unresolved. Mock success is not live acceptance; #48 stays incomplete |

## Testing strategy

Use existing Vitest, Testing Library/user-event, adapter/session doubles and `deferred` helpers. Inject or stub fetch for public REST; no MSW/new packages. Resolve the normal authenticated session and use MemoryRouter for navigation. Use deterministic fake timers and controlled promises, restore all stubs/timers after tests and avoid sleep-based race assertions.

- API tests: independent Lobby/Gameplay configured origins (including supported prefixes), bearer from current session, create/join `{}`, lock bodyless, start `{joinCode}` with no userId/hostId/playerId/matchId; correct code path and signal; valid six-field DTO; malformed success failure; start 200/201 response ID; errors and abort through the shared client. Verify no internal URL or machine scope/credential/storage usage.
- Component workflow: anonymous explicit sign-in, loading, authenticated empty/create/join, exact local validation without fetch, displayed code/count/flags/status, host Open-one waiting, host Open-two lock, host Locked-two start, nonhost disabled controls/explanations, public nonmember state and clipboard success/fallback. Drive transitions with approved public responses, never subject IDs. No mutation before an explicit action.
- Error table: 400/401/403/404/409/503/network/unexpected with safe visible messages. 401 invalidates session and clears displayed feature data without auto-login; 403 keeps session; 409 triggers one refresh without mutation retry; 503 requires explicit user action. Ensure raw backend sentinel detail and HTML never render. Rapid duplicate actions produce a single request.
- Lifecycle races: no overlapping polls while a deferred read is outstanding; expected two-second scheduling; timer/read abortion on unmount/session expiry/logout/replacement; late resolved/rejected requests cannot restore response data, error state or navigation. Resolve an earlier Open poll after successful lock and assert Locked remains; resolve an old-lobby preview after switching and assert only the new lobby remains. Aborted mutation/start does not navigate or revive private state. Cover StrictMode effect setup/cleanup without leaks where practical.
- App tests: existing navigation and all unrelated destination behavior retained; real `/lobby` page mounted; after host Locked start, both 200 and 201 route to exactly the returned `/matches?matchId=...`. Destination is an honest shell placeholder with copyable same-origin link, not fabricated Gameplay data. Guest notification link reaches the existing placeholder via the documented alias; do not test unavailable event/participant screens as implemented features.

## Validation commands and environment

From `src/QuizArena.Client` with existing supported Node/npm and committed lockfile:

```powershell
npm ci
npm run typecheck
npm run lint
npm run test -- --run src/api/lobby.test.ts src/features/lobby/LobbyPage.test.tsx src/features/lobby/LobbyPage.lifecycle.test.tsx src/app/App.test.tsx
npm run test -- --run
npm run build
```

From the worktree root:

```powershell
node contracts/validate-contracts.mjs
git diff --check
```

Root performs required aggregate .NET restore/build-warnaserror/format/tests/coverage and final frontend checks. If npm/Vite fails due to sandbox EPERM/process/cache access, send root the exact command/workdir/log request immediately; workers do not request escalation or count infrastructure failure as product success. Baseline 81 tests in nine files with all frontend gates passing is prerequisite evidence only. Record fresh counts/exits and actual limits under `.dispatcher/runs/12-20261104/child-48/`.

No computer use is required for implementation/component verification. Root owns any later truly necessary live browser work and releases it immediately. A live two-user flow requires real provider/Lobby/Gameplay endpoints and appropriate accounts; it cannot be marked passed from fetch doubles or a shell route. Document steps for create/join/poll/host lock/start, returned-ID navigation and errors, with actual execution pending until dependencies exist.

## Risks, blockers and exclusions

Polling must not race a mutation or survive teardown; disabled JSX alone cannot prevent same-tick duplicate submissions. Stable memoized clients avoid effect churn. A preview is caller-relative safe data, not a full roster, so membership is represented by counts/flags rather than invented names. Clipboard failures require an honest manual fallback. The existing HTTP client already invalidates on 401; coordinate feature cleanup with that lifecycle instead of changing shared authentication.

External #53 blocks live start acceptance. Full match details/participant enforcement, event-based guest opening, delayed event delivery and guest entry from copied links remain responsibilities of the unavailable peer flows; this child provides documented links/waiting/navigation only and makes no claim those flows passed. #24 blocks #49's hard start and is not implemented here. Setup docs and root's project board/partial PR must show #48 implementation separately from live completion; do not use Closes #12/#48/#49 while their acceptance is incomplete.

Exclude backend/security policy changes, Gameplay/Notification UI, fake Gameplay endpoints, cross-service/internal calls, machine secrets/scopes, durable browser response/token storage, dependency upgrades, #49 implementation, fabricated live evidence, commits/publication and human approval. Return implementation-stage PASS where earned while retaining live BLOCKED #53; final human signoff stays pending on the one partial draft PR.
