# Milestone 3: architecture, security and reflection report

Issue [#65](https://github.com/JimRashed/Web-3-Final-Project/issues/65) (D04), epic #7. Team: Jim Rashed, Suheng Li (SuSu-Lisu) and Carter Beasse. Detailed references: [architecture decisions](architecture-decisions.md), [API contracts](api-contracts.md), [authentication](authentication.md), [authorization matrix](authorization-matrix.md), [work allocation](work-allocation.md) and [validation results](validation-results.md).

## 1. Layered architecture

QuizArena moved from one aggregate ASP.NET Core host with a shared `QuizArenaContext` (Milestone 2, now [historical](../architecture-notes.md)) to **six independently runnable .NET 10 services**: Identity, Statistics, QuestionBank, Lobby, Gameplay and Notification. Each service is split into three projects:

| Layer | Owns | May reference |
|---|---|---|
| `QuizArena.<Service>.Domain` | Entities, invariants, domain exceptions, repository interfaces | nothing (no packages, no framework) |
| `QuizArena.<Service>.Data` | `DbContext`, Fluent API configuration, migrations, repositories | its own Domain |
| `QuizArena.<Service>.Api` | DTOs, application services, controllers, JWT policies, exception mapping | its own Data and Domain |

**Enforced, not just documented.** `tests/QuizArena.ArchitectureTests` reads every project file and fails the build if a reference points the wrong way, if a Domain project gains a package, or if a test project references another service. No project references another service's assemblies.

**Data ownership.** Each service owns one SQL Server database (`QuizArena<Service>Db`), one context and its own migration lineage. A model-drift test fails if the model and the latest migration disagree. Cross-service identifiers are opaque strings compared with the binary collation `Latin1_General_100_BIN2`, and timestamps are stored and read back as UTC. Services exchange data only over HTTP. Gameplay calls Lobby and QuestionBank through ports with machine tokens and reports results to Statistics, and Statistics accepts them idempotently (an identical retry returns 200; a conflicting duplicate returns 409).

**Concurrency is designed in.** Business keys have unique indexes, and duplicate-key errors (2601/2627) are treated as "another request won" and recovered by re-reading. The Gameplay match row carries a `rowversion`, so two players answering at the same moment cannot silently overwrite each other.

## 2. Frontend choice

We chose one **React 19 + TypeScript + Vite** client (`src/QuizArena.Client`) because the course requires an independent client layer, and because an OIDC authorization-code flow with PKCE is naturally a browser concern. The client:
- talks to each service's public HTTP API, never to a database;
- keeps tokens **in memory only** (keycloak-js), with nothing in local or session storage or cookies;
- validates every response against the contract before it reaches the UI, so contract drift shows up as an error state instead of undefined behaviour.

Each screen has loading, empty, error and retry states, and 401/403 have their own messages.

## 3. Security

| OAuth role | Component |
|---|---|
| Resource owner | the player |
| Authorization server | Keycloak 26.7.5, realm `quizarena` (`infra/`) |
| Public client | React app `quizarena-client`: authorization code + **PKCE S256 only** |
| Confidential client | Gameplay `quizarena-gameplay`: client credentials for service-to-service calls |
| Resource servers | the six APIs, audience `quizarena-<service>` |

**Token settings** (secrets redacted; values live only in the git-ignored `.env`):
- Access tokens last 5 minutes, and refresh tokens rotate without reuse.
- `fullScopeAllowed` is off for both clients.
- A signed `qa_actor` claim (`user` or `machine`) separates players from services.

Every API validates tokens with JwtBearer: RS256 signature, exact issuer, its own audience, a required `exp`, and `typ=Bearer`, so ID and refresh tokens are rejected. Each endpoint carries a named policy that requires the **exact scope**, the expected client (`azp`) and actor, and a subject. Ownership compares the token subject ordinally. The fallback policy rejects unauthenticated calls, and only `/health` is anonymous. The full endpoint-to-policy mapping is in the [authorization matrix](authorization-matrix.md).

**Security tests.** A shared fixture signs real JWTs with test keys, and every service runs a security matrix. For Identity and Statistics the full matrix also runs against the **real routes**:
- 401 cases: missing, malformed, bad signature, unknown key, expired, no expiry, wrong issuer, wrong audience, ID token;
- 403 cases: missing or prefix/suffix scope, no subject, wrong client, wrong or missing actor;
- rejected writes are checked to persist nothing.

Results are in [validation results](validation-results.md).

## 4. How the work was divided

Each member owned **two services end to end** (domain, persistence, API, React screens and slice tests), plus shared platform tasks. Details are in [work allocation](work-allocation.md).

| Member | Services | Platform |
|---|---|---|
| Jim Rashed | QuestionBank, Lobby | board (P01), contracts (P02), service scaffold (P03), React shell (P07), agents (P08) |
| Carter Beasse | Identity, Statistics | databases and migrations (P04), Keycloak (P05), JWT policies (P06), this report (D04) |
| Suheng Li | Gameplay, Notification | test fixture (P09), CI (P10) |

The contracts came first (P02). With the OpenAPI documents committed, the services and screens could be built in parallel against agreed DTOs.

## 5. Agent assistance and human corrections

We used AI coding agents throughout, following the repository's Research → Plan → Implement → Validate workflow and the Dispatcher (`docs/dispatcher.md`). Separate layer-specific agents were proposed (P08) and then dropped on Larco's advice. Agents were strongest at structure and repetition: six consistent Domain/Data/Api triples, migrations, repositories with the same duplicate-key recovery, and tests written alongside the code.

They were weakest at the parts of a specification that are easy to skim. Teammates' PR reviews, often with throwaway probe tests, caught these real defects. Every test the agent had written was passing at the time.

| PR | Defect caught in review | Outcome |
|---|---|---|
| #74 Notification domain | free-form event payload, and preferences defaulting to *enabled* instead of the contract's *disabled* | fixed before merge |
| #82 Notification data | upsert test reused one `DbContext`, so EF's change tracker hid the real behaviour | test fixed |
| #86 Gameplay data | two players answering at once lost one answer (proved: 1 of 2 stored) | `rowversion` + `MatchConcurrencyException` |
| #87 CI | legacy LocalDB tests failed on the Linux runner | fixed; CI green |
| #93 Statistics endpoints | a timestamp without an offset was read in the server's local time zone | caught by a test; now rejected with 400 |
| #95/#96 Gameplay application/endpoints | a repeated start returned the match to non-hosts; simultaneous starts and simultaneous answers returned 500 | fixes proposed in #107/#108 |
| #100 Match page | page unreachable from the Lobby, guest saw the host's score as "You", a second click left an error page with no recovery | fixes proposed in #109 |
| #90 Notification endpoints | `GET /events` lacked the contract's paging and `userId` 403 rule | merged; open follow-up |
| #111 Gameplay outbox (merged without review) | the token options bound the wrong config keys, and the OAuth response was read as camelCase, so every match start returned 503. Found only by a live two-player run. | fixed in #118 |
| Client (guest flow) | the guest had no in-app way to reach the match | fixed in #121 |
| #97 reflection draft | claimed three human gates per task, which we did not run | draft withdrawn; this report replaces it |

The pattern: agents produce plausible code that passes the tests they wrote themselves. A reviewer reading the acceptance criteria and reasoning about **two requests at once** found what the tests did not.

## 6. How effective the human gates were

The specification called for three gates per task. In practice our instructor allowed the plan gate to be optional near the deadline, so **the effective human gate was one independent teammate review per PR, backed by CI**. Plans were still written (`specs/`) and reviewed by agents. The PR review caught every defect in the table above. CI (`.github/workflows/ci.yml`: backend with a SQL Server container, architecture, security and frontend jobs) turned "tests pass" from a claim into a link. Reviews were most effective when the reviewer ran the branch and wrote a probe test instead of reading the diff alone.

What we would change:
1. Turn each acceptance criterion into a named test before implementation.
2. Run CI from the first service instead of halfway through.
3. Require every PR that touches shared state to include one concurrency test.

## 7. Delivery decisions and known limitations

- **Submission through GitHub, not Azure.** The milestone is delivered as a reviewed GitHub milestone PR with the issue board, CI runs and PR history as evidence. Everything runs locally (LocalDB or SQL Server, and Keycloak in Docker), and no cloud deployment was in scope.
- A full page reload signs the user out, because tokens are held only in memory. This is a deliberate trade-off. A silent `check-sso` is the planned improvement.
- The Gameplay outbox and redelivery (ADR-07) shipped in #111. Live testing found that it could never obtain a machine token, fixed in #118. The end-to-end journeys (D03) now pass on `main`: browser 11/11, API 10/10 and delivery retry 8/8. Still open: the Notification `GET /events` paging follow-up and an outbox lease-fencing improvement. See [validation results](validation-results.md).

**Links:** [issues](https://github.com/JimRashed/Web-3-Final-Project/issues?q=milestone%3A%22Milestone+3+%E2%80%94+Layered+Service+Development%22), [pull requests](https://github.com/JimRashed/Web-3-Final-Project/pulls?q=is%3Apr), [project board](https://github.com/users/CarterBeasse/projects/1) (private to the owner), [CI workflow](../../.github/workflows/ci.yml), plans in [`specs/milestone3/`](../../specs/milestone3/).
