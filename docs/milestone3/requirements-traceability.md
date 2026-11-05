# Milestone 3 requirements traceability (D05)

Issue [#66](https://github.com/JimRashed/Web-3-Final-Project/issues/66), parent F07 #15, epic #7. This document maps each Milestone 3 deliverable to the issues, PRs, tests and docs that prove it, follows one complete trace per member, and lists what is still open.

The per-issue table at the end is **generated from live GitHub data** by `scripts/traceability/build-traceability.py`, which also writes the machine-readable export [`traceability.json`](traceability.json). Rerun it to refresh the table. The curated sections above it are maintained by hand.

## Deliverables and evidence

| Deliverable | Issues | Merged PRs | Evidence |
|---|---|---|---|
| Six independently runnable services, each Domain/Data/Api | P03 #18; each service's T01–T04 | #72, then the per-service slice PRs | `tests/QuizArena.ArchitectureTests` (no wrong-way or cross-service reference, Domain has no packages); [architecture decisions](architecture-decisions.md) |
| Service-owned persistence | P04 #19; each service's T02 | #78, #80, #81, #82, #86, #92, #89 | One database, context and migration lineage per service; model-drift tests in all six; [database operations](database-operations.md) |
| Public contracts | P02 #17 | #70 | `contracts/openapi/*.yaml`; Development OpenAPI documents tested against them (Identity, Statistics); [API contracts](api-contracts.md) |
| OAuth 2.0 / OIDC with Keycloak | P05 #20, P06 #21 | #71, #73 | Authorization code + PKCE S256, client credentials for Gameplay, JwtBearer with exact scope and actor policies; [authentication](authentication.md), [authorization matrix](authorization-matrix.md) |
| React client | P07 #22; each service's T05 | #79, #89, #94, #100, #102, #106, #109 | One screen set per service, responses validated against the contracts; [frontend setup](frontend-setup.md) |
| Tests and CI | P09 #24, P10 #25; each service's T06 | #83, #87, #99, #104, #112, #116 | `.github/workflows/ci.yml` (backend with SQL Server, architecture, security, frontend); [validation results](validation-results.md) |
| Agent workflow | P08 #23 | — (layer agents dropped on Larco's advice) | R-P-I-V and dispatcher agents in `.github/agents`, `.github/skills`; per-issue plans in `specs/` |
| Local run | D01 #62 | #113 (+ #118 open) | `scripts/start-milestone3.ps1`, [runbook](runbook.md) with validation record |
| Legacy data cutover | D02 #63 | — (#115 open) | `scripts/migrate-legacy-data/`, [data cutover](data-cutover.md), 34/34 rehearsal |
| Secured two-player journey | D03 #64 | — | API-level live journey 10/10 (PR #118); the browser journey and retry scenario are still open |
| Report | D04 #65 | #110 | [architecture and reflection](architecture-and-reflection.md) |
| Traceability and board | D05 #66, P01 #16 | this PR | This document, [`traceability.json`](traceability.json), [project board](https://github.com/users/CarterBeasse/projects/1) (private to its owner, so the JSON export is the shareable evidence) |
| Review and signoff | D06 #67 | — | One teammate review per PR (see the "Approved by" column); D06 still open |
| Milestone PR to `main`, branch retained | D07 #68 | **#114**, merged 2026-11-05 00:20 UTC by SuSu-Lisu, merge commit `1b6c8f2683`; CI on `main` passed | `feature/milestone-3-layered-service-development` still exists and `delete_branch_on_merge` is off. #114 merged with a comment review only (no approval), before D02, D03, D05 and D06 were complete |
| Submission bundle | D08 #69 | — | Pending D07 follow-ups and D06 |

## One full trace per member

**Carter Beasse: Statistics.**
- Domain #32 → #77, then data #33 → #81, then application #34 → #85, then endpoints and OpenAPI #35 → #93, then React #36 → #102, then slice coverage #37 → #104.
- Feature #10 is closed with an evidence comment.
- A result recorded by the Gameplay machine client shows up in the player's statistics and on the leaderboard (live checks in #102 and #118).

**Jim Rashed: QuestionBank.**
- Domain #38 → #91, then data #39 → #92, then application #40 → #98, then endpoints #41 → #103, then React #42 → #106, then coverage #43 → #116.
- Feature #11 is closed.
- The live check in the #106 review confirmed answer-free previews, and that a player token is refused on the internal snapshot route (403).

**Suheng Li: Notification.**
- Domain #56 → #74, then data #57 → #82, then application and endpoints #58/#59 → #90, then React #60 → #94, then coverage #61 → #99.
- Feature #14 is closed.
- `MATCH_CREATED` and `MATCH_COMPLETED` delivered by the Gameplay outbox appear in both players' event lists (live journey, #118).

## Audit findings

- **No orphans or missing owners:** all 63 issues have an assignee, and every task has a parent feature.
- **Every member has two complete slices, each with a frontend task and a test task:**
  - Identity and Statistics: Carter.
  - QuestionBank and Lobby: Jim. Lobby #44–#49 was delivered through #89 and #112, and #49 is still open.
  - Gameplay and Notification: Suheng. #55 has an open PR, #117.
- **Closed without a code PR, and why:**
  - #16 (board and ownership map): the board and issues are the deliverable.
  - #23 (layer agents): dropped on Larco's advice, recorded in the closing comment of #97.
- **Merged but deliberately still open** (they wait on an acceptance step, not on code):
  - #62: a teammate still has to follow the runbook from a clean checkout.
  - #65: the D03 results still need to be added to the report.
  - #49: Jim's own acceptance.
- **Defects found after merge** (open fix PRs):
  - #111: Gameplay could never obtain a machine token, so every match start returned 503. Fix in #118.
  - #90: `GET /events` lacks paging and the `userId` 403 rule. Open follow-up.
- **The milestone PR (#114) merged before its prerequisites** (D02, D03, D05, D06). The fixes in #118 and #115 will reach `main` through a follow-up milestone PR.

## Still open (named explicitly)

| Item | Owner | Blocking |
|---|---|---|
| #115 D02 cutover tool and retiring the aggregate host | Carter | review |
| #118 Gameplay machine-token fixes, start-script secret | Carter | review; required for any real match |
| #117 / #55 Gameplay slice coverage | Suheng | review |
| #64 D03 browser two-player journey and delivery-retry scenario | Suheng | #118 |
| #67 D06 read-only review and signoff | Suheng | the items above |
| #68 D07 follow-up milestone PR carrying the fixes | Carter | D06, and the team's go-ahead |
| #69 D08 submission bundle | Carter | D07 |
| #90 follow-up: `GET /events` paging and `userId` rule | Suheng | — |

## Per-issue map

<!-- BEGIN GENERATED ISSUE MAP -->

Generated by `scripts/traceability/build-traceability.py` from live GitHub data. Export: [`traceability.json`](traceability.json).

**63 issues** (1 epic, 8 features, 54 tasks); **49 closed**.

| Check | Issues |
|---|---|
| Without an assignee | — |
| Task without a parent feature | — |
| Task closed without a merged PR | #16, #23 |
| Task still open although a PR merged (needs acceptance or human gate) | #49, #62, #65 |

| # | ID | Title | State | Assignee | Parent | Merged PRs | Open PRs | Approved by |
|---|---|---|---|---|---|---|---|---|
| #7 | E00 | Complete Milestone 3: Layered Service Development | open | JimRashed | — | #70, #71, #72, #73, #74, #75, #76, #77, #78, #79, #80, #81, #82, #83, #84, #85, #86, #87, #88, #89, #90, #93, #94, #95, #96, #99, #100, #102, #104, #106, #110, #112, #113, #116 | #115 | CarterBeasse, JimRashed, SuSu-Lisu |
| #8 | F00 | Establish contracts, architecture, security, frontend foundations and agent workflow | closed | JimRashed | #7 | #70, #71, #72, #73, #78, #79, #83, #87 | — | CarterBeasse, JimRashed |
| #9 | F01 | Deliver the independently runnable Identity service and connected React views | closed | CarterBeasse | #7 | #76, #80, #84, #88, #102, #104 | — | JimRashed, SuSu-Lisu |
| #10 | F02 | Deliver the independently runnable Statistics service and connected React views | closed | CarterBeasse | #7 | #77, #81, #85, #93, #102, #104 | — | SuSu-Lisu |
| #11 | F03 | Deliver the independently runnable QuestionBank service and connected React views | closed | JimRashed | #7 | #106, #116 | — | CarterBeasse |
| #12 | F04 | Deliver the independently runnable Lobby service and connected React views | open | JimRashed | #7 | #89, #112 | — | CarterBeasse, SuSu-Lisu |
| #13 | F05 | Deliver the independently runnable Gameplay service and connected React views | open | SuSu-Lisu | #7 | #75, #86, #95, #96, #100 | — | CarterBeasse, JimRashed |
| #14 | F06 | Deliver the independently runnable Notification service and connected React views | closed | SuSu-Lisu | #7 | #74, #82, #90, #94, #99 | — | CarterBeasse, JimRashed |
| #15 | F07 | Integrate, validate, document and submit the reviewed GitHub milestone | open | CarterBeasse | #7 | #113 | #115 | SuSu-Lisu |
| #16 | P01 | Publish the milestone board, ownership map and GitHub delivery rules | closed | JimRashed | #8 | — | — | — |
| #17 | P02 | Approve service boundaries, public contracts, identity mapping and security policies | closed | JimRashed | #8 | #70 | — | — |
| #18 | P03 | Scaffold six independently runnable layered services and migrate the solution graph | closed | JimRashed | #8 | #72 | — | CarterBeasse |
| #19 | P04 | Establish service-owned EF configuration, migrations and safe database boundaries | closed | CarterBeasse | #8 | #78 | — | JimRashed |
| #20 | P05 | Configure a reproducible OIDC provider and client/service identities | closed | CarterBeasse | #8 | #71 | — | JimRashed |
| #21 | P06 | Apply real JWT validation and reusable scope policies to all six APIs | closed | CarterBeasse | #8 | #73 | — | — |
| #22 | P07 | Build the shared React frontend shell, OIDC session and HTTP clients | closed | JimRashed | #8 | #79 | — | CarterBeasse |
| #23 | P08 | Add constrained layer agents and reconcile the repository skills with Milestone 3 | closed | JimRashed | #8 | — | — | — |
| #24 | P09 | Create isolated per-service test hosts and a real bearer-token test fixture | closed | SuSu-Lisu | #8 | #78, #83 | — | CarterBeasse, JimRashed |
| #25 | P10 | Add CI gates for architecture, backend, frontend and security tests | closed | SuSu-Lisu | #8 | #87 | — | CarterBeasse |
| #26 | S01-T01 | Implement Identity domain models and pure business rules | closed | CarterBeasse | #9 | #76 | — | — |
| #27 | S01-T02 | Implement Identity EF mappings, migrations and repositories | closed | CarterBeasse | #9 | #78, #80 | — | JimRashed |
| #28 | S01-T03 | Implement Identity API DTOs and application orchestration | closed | CarterBeasse | #9 | #84 | — | JimRashed |
| #29 | S01-T04 | Expose and secure Identity REST endpoints with OpenAPI | closed | CarterBeasse | #9 | #88 | — | SuSu-Lisu |
| #30 | S01-T05 | Build Identity React screens and REST integration | closed | CarterBeasse | #9 | #102 | — | SuSu-Lisu |
| #31 | S01-T06 | Complete Identity vertical-slice, authorization and regression coverage | closed | CarterBeasse | #9 | #104 | — | SuSu-Lisu |
| #32 | S02-T01 | Implement Statistics domain models and pure business rules | closed | CarterBeasse | #10 | #77 | — | — |
| #33 | S02-T02 | Implement Statistics EF mappings, migrations and repositories | closed | CarterBeasse | #10 | #78, #81 | — | JimRashed |
| #34 | S02-T03 | Implement Statistics API DTOs and application orchestration | closed | CarterBeasse | #10 | #85 | — | SuSu-Lisu |
| #35 | S02-T04 | Expose and secure Statistics REST endpoints with OpenAPI | closed | CarterBeasse | #10 | #93 | — | — |
| #36 | S02-T05 | Build Statistics React screens and REST integration | closed | CarterBeasse | #10 | #102 | — | SuSu-Lisu |
| #37 | S02-T06 | Complete Statistics vertical-slice, authorization and regression coverage | closed | CarterBeasse | #10 | #104 | — | SuSu-Lisu |
| #38 | S03-T01 | Implement QuestionBank domain models and pure business rules | closed | JimRashed | #11 | #91 | — | — |
| #39 | S03-T02 | Implement QuestionBank EF mappings, migrations and repositories | closed | JimRashed | #11 | #78, #92 | — | JimRashed |
| #40 | S03-T03 | Implement QuestionBank API DTOs and application orchestration | closed | JimRashed | #11 | #98 | — | CarterBeasse |
| #41 | S03-T04 | Expose and secure QuestionBank REST endpoints with OpenAPI | closed | JimRashed | #11 | #103 | — | CarterBeasse |
| #42 | S03-T05 | Build QuestionBank React screens and REST integration | closed | JimRashed | #11 | #106 | — | CarterBeasse |
| #43 | S03-T06 | Complete QuestionBank vertical-slice, authorization and regression coverage | closed | JimRashed | #11 | #116 | — | CarterBeasse |
| #44 | S04-T01 | Implement Lobby domain models and pure business rules | closed | JimRashed | #12 | #89 | — | CarterBeasse, SuSu-Lisu |
| #45 | S04-T02 | Implement Lobby EF mappings, migrations and repositories | closed | JimRashed | #12 | #78, #89 | — | CarterBeasse, JimRashed, SuSu-Lisu |
| #46 | S04-T03 | Implement Lobby API DTOs and application orchestration | closed | JimRashed | #12 | #89 | — | CarterBeasse, SuSu-Lisu |
| #47 | S04-T04 | Expose and secure Lobby REST endpoints with OpenAPI | closed | JimRashed | #12 | #89 | — | CarterBeasse, SuSu-Lisu |
| #48 | S04-T05 | Build Lobby React screens and REST integration | closed | JimRashed | #12 | #89 | — | CarterBeasse, SuSu-Lisu |
| #49 | S04-T06 | Complete Lobby vertical-slice, authorization and regression coverage | open | JimRashed | #12 | #89, #112 | — | CarterBeasse, SuSu-Lisu |
| #50 | S05-T01 | Implement Gameplay domain models and pure business rules | closed | SuSu-Lisu | #13 | #75 | — | CarterBeasse, JimRashed |
| #51 | S05-T02 | Implement Gameplay EF mappings, migrations and repositories | closed | SuSu-Lisu | #13 | #78, #86 | — | CarterBeasse, JimRashed |
| #52 | S05-T03 | Implement Gameplay API DTOs and application orchestration | closed | SuSu-Lisu | #13 | #95, #107, #111 | #118 | SuSu-Lisu |
| #53 | S05-T04 | Expose and secure Gameplay REST endpoints with OpenAPI | closed | SuSu-Lisu | #13 | #96, #108 | — | JimRashed, SuSu-Lisu |
| #54 | S05-T05 | Build Gameplay React screens and REST integration | closed | SuSu-Lisu | #13 | #100, #109 | — | SuSu-Lisu |
| #55 | S05-T06 | Complete Gameplay vertical-slice, authorization and regression coverage | open | SuSu-Lisu | #13 | — | #117 | — |
| #56 | S06-T01 | Implement Notification domain models and pure business rules | closed | SuSu-Lisu | #14 | #74 | — | CarterBeasse, JimRashed |
| #57 | S06-T02 | Implement Notification EF mappings, migrations and repositories | closed | SuSu-Lisu | #14 | #78, #82 | — | CarterBeasse, JimRashed |
| #58 | S06-T03 | Implement Notification API DTOs and application orchestration | closed | SuSu-Lisu | #14 | #90 | — | JimRashed |
| #59 | S06-T04 | Expose and secure Notification REST endpoints with OpenAPI | closed | SuSu-Lisu | #14 | #90 | — | JimRashed |
| #60 | S06-T05 | Build Notification React screens and REST integration | closed | SuSu-Lisu | #14 | #94 | — | CarterBeasse, JimRashed |
| #61 | S06-T06 | Complete Notification vertical-slice, authorization and regression coverage | closed | SuSu-Lisu | #14 | #99 | — | CarterBeasse |
| #62 | D01 | Run the six-service application and frontend from a documented local environment | open | CarterBeasse | #15 | #113 | #118 | SuSu-Lisu |
| #63 | D02 | Migrate legacy data safely and retire the aggregate host and shared context | open | CarterBeasse | #15 | — | #115 | — |
| #64 | D03 | Verify the complete secured two-player journey and delivery retries | open | SuSu-Lisu | #15 | — | #118 | — |
| #65 | D04 | Write the Milestone 3 architecture, security and reflection report | open | CarterBeasse | #15 | #110 | — | SuSu-Lisu |
| #66 | D05 | Audit issue traceability, acceptance evidence and board completeness | open | CarterBeasse | #15 | — | — | — |
| #67 | D06 | Complete read-only review and human signoff for the milestone candidate | open | SuSu-Lisu | #15 | — | — | — |
| #68 | D07 | Merge the retained milestone branch through a reviewed GitHub milestone PR | open | CarterBeasse | #15 | — | — | — |
| #69 | D08 | Publish the final GitHub milestone submission bundle and board export | open | CarterBeasse | #15 | — | — | — |

<!-- END GENERATED ISSUE MAP -->
