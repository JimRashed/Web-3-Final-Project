# P02 authorization matrix

Proposed; human approval pending. All paths below are relative to the owning service origin. Audience is `quizarena-<service>`. Exact trusted issuer, signature, expiry/not-before and audience validation precede scope/ownership checks.

Keycloak public client `quizarena-client` receives only user scopes via authorization-code PKCE S256. Confidential client `quizarena-gameplay` receives only `lobbies.verify questions.play results.write events.write` and the four corresponding audiences via client credentials. Machine credentials stay server-side. Obtain destination tokens scoped to that resource; do not forward browser tokens to internal routes.

Use trusted signed `azp` to identify the configured client. Machine policy requires exact scope token AND `azp=quizarena-gameplay` AND a configured service-account subject belonging to that client. User policy requires `azp=quizarena-client`, a user subject and rejects service-account identities. Keycloak P05 must configure these claim mappings and allowlists explicitly; do not infer actor kind from subject text prefixes. Extra scopes never turn machine credentials into user credentials. Scope matching is whitespace-delimited exact membership, never substring matching.

| Service | Method / route | Actor | Named policy | Ownership / resource condition |
|---|---|---|---|---|
| identity | GET `/api/users/me` | user | profiles.read | validated issuer/subject |
| identity | PUT `/api/users/me` | user | profiles.write | validated issuer/subject |
| identity | POST `/api/auth/register` | public | public | none |
| identity | POST `/api/auth/login` | public | public | none |
| identity | GET `/api/users/{id}` | public | public | none |
| identity | GET `/health` | public | public | none |
| statistics | POST `/api/results` | machine | results.write | none |
| statistics | GET `/api/players/{userId}/stats` | user | stats.read | path userId equals validated subject |
| statistics | GET `/api/players/{userId}/matches` | user | stats.read | path userId equals validated subject |
| statistics | GET `/api/leaderboard` | user | leaderboard.read | none |
| statistics | GET `/health` | public | public | none |
| questionbank | GET `/api/questions/generate` | user | questions.read | none |
| questionbank | POST `/internal/question-sets` | machine | questions.play | none |
| questionbank | GET `/health` | public | public | none |
| lobby | POST `/api/lobbies` | user | lobbies.write | none |
| lobby | GET `/api/lobbies/{joinCode}` | user | lobbies.read | none |
| lobby | POST `/api/lobbies/{joinCode}/join` | user | lobbies.write | none |
| lobby | POST `/api/lobbies/{joinCode}/lock` | user | lobbies.write | caller is lobby host |
| lobby | GET `/internal/lobbies/{joinCode}` | machine | lobbies.verify | none |
| lobby | GET `/health` | public | public | none |
| gameplay | POST `/matches` | user | matches.write | verified locked lobby host |
| gameplay | GET `/matches/{matchId}` | user | matches.read | match participant |
| gameplay | GET `/matches/{matchId}/next-question` | user | matches.read | match participant |
| gameplay | POST `/matches/{matchId}/answers` | user | matches.write | match participant |
| gameplay | GET `/health` | public | public | none |
| notification | POST `/events` | machine | events.write | none |
| notification | GET `/events` | user | events.read | validated subject; optional legacy userId must equal caller |
| notification | GET `/users/{userId}/preferences` | user | preferences.read | path userId equals validated subject |
| notification | PUT `/users/{userId}/preferences` | user | preferences.write | path userId equals validated subject |
| notification | GET `/health` | public | public | none |

## Security cases and evaluation order

For **every user row**: valid matching audience+scope+actor and required ownership reaches the documented success; absent/expired/wrong issuer/wrong audience/invalid signature token401; valid user token missing exact scope403; machine token403. For **every machine row**: allowlisted machine with destination scope reaches success; browser token403 even with accidentally granted scope; unallowlisted client403; invalid/missing token401. An authentication failure includes WWW-Authenticate: Bearer. Health and retired rows require no token; health200 and retired410 even if an irrelevant bearer header is supplied.

For **each self-only row** (Statistics stats/history, Notification preferences, optional events userId): caller player-a requesting player-b403 before data lookup, including nonexistent player-b. GET /api/users/me and PUT /api/users/me derive issuer/subject exclusively from claims. Reject extra userId/hostId/playerId properties in user mutation bodies400; never use them to select an actor.

For **each match read/answer row**: nonexistent match404; existing match with outsider403; participant follows state rules. This intentionally reveals existence to authenticated scoped outsiders, but no match details. For **lock/create-match**: nonexistent lobby404; existing nonhost403 before state validation; host with invalid state409. Lobby preview is available to any scoped user and exposes member count and caller-relative booleans, not member subjects. Internal Lobby lookup is machine-only and may expose identities for verification.

Negative input examples: count=0 or51 ->400; page=0/pageSize=101 ->400; top=101 ->400; joinCode=abc123 ->400; displayName whitespace ->400; malformed GUID questionId ->400. New result score6 ->400; conflicting replay ->409. User token calling /internal/question-sets ->403; public Question/CurrentQuestion response containing correctAnswer fails contract validation. Duplicate/stale/finished match answer ->409; GET next-question after finish409. Preference GET with no row returns false/false without persisting.

Error precedence: authenticate -> authorize scope/actor -> validate syntax (400) -> check claim-based self ownership -> lookup -> resource ownership -> business state. Public retired routes skip all body validation; public health ignores irrelevant query/body and reports only liveness. Backend authorization is mandatory regardless of frontend guards.

## Client handling

401 prompts sign-in;403 shows a permission message without retry loops;400 maps safe field/message details;404 missing-resource UI;409 refreshes state and explains conflict;503 offers a bounded user retry; safe500 shows traceId for support. Tokens stay in memory and never appear in URLs/logs/localStorage. Browser must not request machine scopes or call internal APIs. No public answer key, password/hash, machine secret or issuer administration data is returned.
