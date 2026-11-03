# P02 API and frontend contracts

**Proposed; final team approval pending.** Machine-readable source is the six [OpenAPI documents](../../contracts/openapi/identity.yaml) under `contracts/openapi/`. Each is self-contained OpenAPI3.0.3, serialized as JSON-compatible YAML1.2 for dependency-free checking. [Authorization](authorization-matrix.md) and [architecture](architecture-decisions.md) add normative rules that schema alone cannot enforce. Historical `docs/api-service-contracts.md` remains the16-operation baseline.

## Operation inventory

| Service | Method / route | Operation ID | Declared outcomes |
|---|---|---|---|
| identity | GET `/api/users/me` | getMyProfile | 200, 401, 403, 404, 500, 503 |
| identity | PUT `/api/users/me` | updateMyProfile | 200, 400, 401, 403, 500, 503 |
| identity | POST `/api/auth/register` | retiredRegister | 410 |
| identity | POST `/api/auth/login` | retiredLogin | 410 |
| identity | GET `/api/users/{id}` | retiredProfileById | 410 |
| identity | GET `/health` | identityHealth | 200 |
| statistics | POST `/api/results` | recordResult | 200, 400, 401, 403, 409, 500, 503 |
| statistics | GET `/api/players/{userId}/stats` | getMyStats | 200, 400, 401, 403, 500, 503 |
| statistics | GET `/api/players/{userId}/matches` | getMyHistory | 200, 400, 401, 403, 500, 503 |
| statistics | GET `/api/leaderboard` | getLeaderboard | 200, 400, 401, 403, 500, 503 |
| statistics | GET `/health` | statisticsHealth | 200 |
| questionbank | GET `/api/questions/generate` | generateQuestions | 200, 400, 401, 403, 500, 503 |
| questionbank | POST `/internal/question-sets` | createQuestionSnapshot | 200, 400, 401, 403, 409, 500, 503 |
| questionbank | GET `/health` | questionbankHealth | 200 |
| lobby | POST `/api/lobbies` | createLobby | 201, 400, 401, 403, 500, 503 |
| lobby | GET `/api/lobbies/{joinCode}` | getLobbyPreview | 200, 400, 401, 403, 404, 500, 503 |
| lobby | POST `/api/lobbies/{joinCode}/join` | joinLobby | 200, 400, 401, 403, 404, 409, 500, 503 |
| lobby | POST `/api/lobbies/{joinCode}/lock` | lockLobby | 200, 400, 401, 403, 404, 409, 500, 503 |
| lobby | GET `/internal/lobbies/{joinCode}` | verifyLobby | 200, 400, 401, 403, 404, 500, 503 |
| lobby | GET `/health` | lobbyHealth | 200 |
| gameplay | POST `/matches` | createMatch | 200, 201, 400, 401, 403, 404, 409, 500, 503 |
| gameplay | GET `/matches/{matchId}` | getMatch | 200, 400, 401, 403, 404, 500, 503 |
| gameplay | GET `/matches/{matchId}/next-question` | getCurrentQuestion | 200, 400, 401, 403, 404, 409, 500, 503 |
| gameplay | POST `/matches/{matchId}/answers` | submitAnswer | 200, 400, 401, 403, 404, 409, 500, 503 |
| gameplay | GET `/health` | gameplayHealth | 200 |
| notification | POST `/events` | createEvent | 202, 400, 401, 403, 409, 500, 503 |
| notification | GET `/events` | getMyEvents | 200, 400, 401, 403, 500, 503 |
| notification | GET `/users/{userId}/preferences` | getMyPreferences | 200, 400, 401, 403, 500, 503 |
| notification | PUT `/users/{userId}/preferences` | updateMyPreferences | 200, 400, 401, 403, 500, 503 |
| notification | GET `/health` | notificationHealth | 200 |

All JSON property names camelCase; UTF-8 application/json. Safe errors use application/problem+json with type,title,status,detail,traceId; never stack traces, token/credential contents or SQL errors.400 invalid input,401 invalid/missing authentication,403 valid but forbidden,404 missing,409 duplicate/state conflict,410 retired,503 dependency unavailable,500 unexpected safe failure. Cancellation propagates; disconnected callers are not converted to500.

No public entity serialization: Api maps DTOs. User mutation objects reject unknown properties400. Nonblank strings must contain a non-whitespace character; field limits are normative in schemas. UTC timestamps use RFC3339 Z form. GUIDs use canonical hyphenated text. New MatchIds are UUID strings, but MatchId wire type permits any nonblank legacy string up to100 characters. userId/subject is case-sensitive opaque string up to200.

## Identity

GET /me reads profile or404 if no local profile. PUT /me upserts displayName, returning200 and a stable internal Guid id; issuer and subject come only from claims. displayName1..80 trimmed characters, whitespace-only invalid, case preserved. No username uniqueness requirement on display names. Client loads profile after login and prompts for display name if404; provider preferred_username may prefill locally but is not an identity mapping. All three legacy endpoints always410 and never process credentials.

## Statistics

POST results is machine-only, accepts score0..5, won and original playedAt UTC. Results are immutable with unique (configured issuer,matchId,userId); typed equality across score/won/playedAt yields200 empty on replay, changed content409. Missing results yield zero totals/empty history200. History response is an array, newest playedAt first then matchId ordinal ascending. page>=1(default1), pageSize1..100(default20), beyond end returns[]. Leaderboard top1..100(default10), totalScore descending, wins descending, userId ordinal ascending; empty[]. Historical score fields are nonnegative without upper5 bound.

## QuestionBank

Public generate returns up to count distinct valid questions without replacement (1..50/default10); empty catalog[]. Public question has id,prompt,category,exactly four distinct nonblank options; never correctAnswer. Internal question-set returns exactly five distinct valid immutable snapshots, including correctAnswer equal ordinally to one option; insufficient catalog409. Questions are validated before selection; no partial snapshots. Internal contract must be excluded from browser generation and Swagger samples; backend controlled clients only.

## Lobby

Six uppercase alphanumeric join codes, unique while retained; generate server-side with collision retry. Creation body{}; creator becomes host and first member, Open,201 with Location. Preview contains lobbyId,joinCode,status,memberCount,isMember,isHost only, safe for scoped nonmembers. Subject IDs are only exposed by machine Provision lookup.

Join body{} uses claims. Open existing member joins are idempotent200; otherwise Open with one member accepts distinct second user. Full or Locked409. Lock requires host403 otherwise, two distinct members409 otherwise; Open transitions Locked, repeated host lock of Locked returns200. No leave/unlock/host-transfer endpoint in milestone. Internal lookup returns hostUserId and1..2 playerIds with state; Gameplay specifically requires Locked and exactly2 including host. Case-invalid join code400, nonexistent404.

## Gameplay

POST matches body{joinCode} derives host from validated identity. Verify via Lobby HTTP with machine policy, then get five QuestionBank snapshots; dependency failure503, invalid upstream contract503, not-found lobby404, nonhost403, unlocked/invalid membership409. Persist one match per stable lobbyId and two creation notification deliveries atomically. playerA is verified host; playerB other member. Concurrency uniqueness ensures one winner insert; losing create returns existing match200 after authorization recheck, first201+Location. Repeat creation of same lobby never fetches a new question set once existing match is found, but must still verify caller ownership.

Match ACTIVE has currentQuestionIndex0..4; FINISHED index5, completedAt set, winnerUserId nullable and draw true exactly for tie. GET current-question does not advance; finished409. Answer must use current question GUID and one exact option (ordinal, no trimming/case-folding answer); unknown option400. Existing match outsider403; stale question, duplicate player's round answer, or finished match409. Transaction stores each player answer once and increments one point if correct. First answer returns roundComplete=false; second returns true and advances one round; fifth second answer sets finished=true. Concurrent answers cannot double-score/advance. Existing player's prior answer correctness is not revealed through question reads.

Completion atomically persists original completion UTC, scores, outcome, two immutable result deliveries and two MATCH_COMPLETED events. Each player result won=true only for sole higher score; draw gives both won=false. Failures after this commit do not change completed response into failure. See architecture worker retry/lease rules.

## Notification

POST events persists before202; EventId generated once by Gameplay is key, immutable payload equality returns original Event (including createdAt), changed replay409. Issuer context is configured, recipient subject is trusted only from allowlisted Gameplay machine. No outbound email/push delivery.

MATCH_CREATED payload requires only matchId and rejects winnerUserId/draw. MATCH_COMPLETED requires matchId,winnerUserId (nullable),draw: draw=true iff winnerUserId=null; draw=false requires winnerUserId equal one of match participants (Gameplay guarantees this; Notification validates nonblank and outcome consistency without cross-service lookup). Fields additional to the typed payload are rejected400. Schema models shared fields; these conditional invariants are normative application checks.

GET events resolves caller; optional legacy userId must equal caller or403. Array ordered createdAt descending then eventId ordinal ascending, same page/pageSize limits; empty[]. GET preferences returns stored values or false/false default without insert; PUT is idempotent upsert, both booleans required, subject from claims/path equality. Preserve existing user preference values on migration; only absent rows get new false/false defaults.

## Application interfaces and dependency seams

All interfaces below belong to each service's Api/Application; each method returns Task<T> (or Task for empty result) and takes CancellationToken last. HTTP DTOs live in Api/Contracts. AuthenticatedActor (issuer,subject,actorKind) is constructed from validated claims, never body binding. Domain repository ports take pure owned entities/value types; Data implements those ports.

| Own Api interface | Operations (Async suffix on each) | Peer adapter |
|---|---|---|
| IProfileApplication | GetMe(actor), UpsertMe(actor,displayName) | none; IdP is authority, no password handling |
| IStatisticsApplication | RecordResult(machine,request), GetStats(actor), GetHistory(actor,page,pageSize), GetLeaderboard(actor,top) | none |
| IQuestionApplication | Generate(actor,count), CreateSnapshot(machine,count) | none |
| ILobbyApplication | Create(actor), Preview(actor,joinCode), Join(actor,joinCode), Lock(actor,joinCode), Verify(machine,joinCode) | none |
| IGameplayApplication | Create(actor,joinCode), Get(actor,matchId), CurrentQuestion(actor,matchId), SubmitAnswer(actor,matchId,questionId,answer) | ILobbyClient.VerifyAsync; IQuestionBankClient.CreateSnapshotAsync; IStatisticsClient.RecordResultAsync; INotificationClient.PublishEventAsync |
| INotificationApplication | CreateEvent(machine,request), GetEvents(actor,page,pageSize), GetPreferences(actor), UpdatePreferences(actor,emailEnabled,pushEnabled) | none |

Adapters live in Gameplay.Api/Clients, own peer DTO copies or generated contract models inside that Api, and obtain destination-scoped machine tokens. Domain does not know HTTP status codes, controllers, claims principal, EF or peer DTOs. Map typed Domain errors in Api.

## React routes and behavior

| Route | Feature path / page | API and behavior |
|---|---|---|
| /profile | identity/ProfilePage.tsx | IdP sign-in/out, GET/PUT /me, display-name form; no password input |
| /statistics | statistics/StatisticsPage.tsx | Self totals/history, pagination and empty state; no result-submit form |
| /leaderboard | statistics/LeaderboardPage.tsx | Authorized top selector1..100, stable rankings |
| /questions | questionbank/QuestionBankPage.tsx | count selector1..50, preview/regenerate options/category; no answers or internal calls |
| /lobbies | lobby/LobbyPage.tsx | Create/join by code, safe preview, host-only lock/start action; start calls Gameplay POST matches |
| /matches/:matchId | gameplay/MatchPage.tsx | State/current question, answer once, show scores/waiting/finished/draw; never send userId or matchId creation |
| /notifications | notification/NotificationsPage.tsx | Self paged events; escaped structured content, no raw HTML |
| /preferences | notification/PreferencesPage.tsx | Read/update booleans, explain delivery is not implemented |
| /auth/callback | shared OIDC shell | PKCE/state/nonce handling, replace URL after completion, tokens in memory |

Default authenticated route/profile; unauthenticated routes offer IdP sign-in. Guard all feature pages, but backend remains authority. Each view has loading, empty, safe error/retry, validation,401 sign-in and403 permission states. Match polling pauses while hidden and cancels in-flight requests on unmount; never advances by polling. Configure a modest2-second foreground poll with no overlapping requests. No SignalR.

### Host and guest entry into the same match

After the host locks the lobby, the host's start action sends POST /matches with joinCode. The response supplies matchId; the host navigates to `/matches/:matchId` and sees a copyable same-origin match link. The guest's Lobby waiting state links to `/notifications`, where their own MATCH_CREATED event supplies payload.matchId and an **Open match** action. The event must belong to the signed-in guest; the client does not infer a match from a lobby code or another user's event. Opening either the event link or host-shared link first calls GET /matches/{matchId}; the server independently verifies the signed-in caller is a participant before showing match details/questions.

Notification delivery can lag behind match creation. The guest sees a pending/waiting message and can refresh/retry their event list; absence of an event is not proof that creation failed. The host can share the returned match link as a fallback while durable delivery retries continue. A guest with multiple events chooses the intended event explicitly; there is no automatic lobby-to-match correlation claim, guessed identifier or extra lookup endpoint. A link never grants membership: an outsider receives403 and a missing match404. Lobby, Gameplay and Notification UI implementation/tests must demonstrate both event-based and copied-link guest entry, delayed-event waiting, and outsider rejection.

## Baseline transition crosswalk

| Existing route(s) | Target change |
|---|---|
| POST auth/register, auth/login; GET users/{id} |410; provider login and users/me |
| GET questions/generate | Options added, bounded count, protected; answers still private |
| POST lobbies/{joinCode}/lock | Host authorization, two-member rule, safe response instead of public player IDs |
| POST matches | Client match/player IDs replaced by joinCode; server match ID and verified roster |
| GET matches/{matchId}, next-question | Participant authorization, persisted snapshots, finished409 |
| POST matches/{matchId}/answers | Remove userId, GUID question, per-player current-round enforcement |
| POST api/results | Machine-only, opaque subject/string match, playedAt, immutable replay |
| GET players/{userId}/stats, matches | Opaque self subject, history pagination, protected |
| GET leaderboard | Authorized, bounded top, stable tie-break |
| POST events | Machine-only EventId and typed payload replace payloadJson string |
| GET events | Claim-selected self, optional legacy matching userId only, pagination |
| PUT users/{userId}/preferences | Self-only, add GET defaults false/false |

This maps all16 baseline operations; remaining target routes and six health endpoints are additions. Retention/mapping/rollback details are in identity-migration.md.

## Example and leaf traceability

Every operation has a machine-readable successful example (except intentionally empty200 results); errors contain ProblemDetails examples. Authorization matrix supplies every operation's positive/negative actor cases; lifecycle paragraphs define state conflicts. Run the contract checker for schema bounds, refs, input spoofing and public-answer closure checks; it does not execute the service.

| Scope | Issues | Contract coverage |
|---|---|---|
| Identity Domain/Data/Api/HTTP/UI/tests | #26–31 (S01-T01..06) | Profile/identity ledger, profile policies and routes, credential retirement |
| Statistics Domain/Data/Api/HTTP/UI/tests | #32–37 (S02-T01..06) | Immutable results, aggregates/history/ranking, self policies, pagination |
| QuestionBank Domain/Data/Api/HTTP/UI/tests | #38–43 (S03-T01..06) | Four options/correct key invariant, public/internal split, exact snapshot |
| Lobby Domain/Data/Api/HTTP/UI/tests | #44–49 (S04-T01..06) | Membership/state/host rules, stable IDs, safe preview and verification |
| Gameplay Domain/Data/Api/HTTP/UI/tests | #50–55 (S05-T01..06) | Five rounds, concurrency, snapshots, outbox and retries |
| Notification Domain/Data/Api/HTTP/UI/tests | #56–61 (S06-T01..06) | Typed/idempotent events, self read, preferences, no external delivery |
| P03/P04 | #18–19 | Layer/table ownership, six hosts/databases, safe transfer |
| P05/P06 | #20–21 | Provider claims/audience/scopes, user/machine discrimination, errors |
| P07/P08 | #22–23 | React routes/client boundaries; scoped agents and owned paths |
| P09/P10 | #24–25 | HTTP integration and contract checks, reproducible configuration |
| D01–D08 | #62–69 | End-to-end two-player flow, validation/security, demo/reflection and reviewed delivery |

All service T01 pure rule tests, T02 relational tests, T03 orchestration tests, T04 HTTP/auth tests, T05 UI tests and T06 audits must assert their row's contract. P02 does not defer those tests to the last task. Three human approvals remain a final prerequisite to implementation fan-out.
