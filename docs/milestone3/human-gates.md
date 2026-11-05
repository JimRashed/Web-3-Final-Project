# Milestone 3 — human gate record

Issue [#67](https://github.com/JimRashed/Web-3-Final-Project/issues/67) (D06), parent F07 #15, epic #7.

This is the signoff log for the milestone: every pull request that reached the milestone branch or
`main`, who reviewed it, and what the review concluded. It is generated from the GitHub reviews API
on 2026-11-05, so it records what actually happened rather than what was intended.

## The gates this milestone asked for

| Gate | Question | Evidence |
|---|---|---|
| **1 — plan** | Was the design reviewed before code? | `specs/milestone3/<issue>-plan.md` committed with the change (e.g. `52-plan.md`, `52-part2-plan.md`, `55-plan.md`, `63-plan.md`, `66-plan.md`, `69-plan.md`) |
| **2 — owner verification** | Did the owner run it and inspect the diff? | Validation commands and results in the PR body, plus `docs/milestone3/validation-results.md` and the runbook's recorded runs |
| **3 — independent review** | Did somebody who did not write it approve it? | The `APPROVED` review recorded below |

The rule the team followed: the approving reviewer must differ from the PR author, and a review
comment is not an approval.

## Per-PR record

Verdict is the strongest review state recorded on the PR, not the intent behind it.

| PR | Title | Author | Verdict | Merged |
|---|---|---|---|---|| #70 | feat: define milestone 3 service contracts (#17) | @JimRashed | commented (CarterBeasse) | 2026-11-03 |
| #71 | feat: configure local Keycloak OIDC provider and client identities (#20) | @CarterBeasse | approved (JimRashed) | 2026-11-03 |
| #72 | feat: scaffold six service hosts (#18) | @JimRashed | approved (CarterBeasse) | 2026-11-03 |
| #73 | feat: validate JWTs and enforce scope policies in all six APIs (#21) | @CarterBeasse | none | 2026-11-03 |
| #76 | feat(identity): add PlayerProfile domain model and rules (#26) | @CarterBeasse | none | 2026-11-03 |
| #77 | feat(statistics): add MatchResult domain model and ranking rules (#32) | @CarterBeasse | none | 2026-11-03 |
| #78 | feat: add service-owned databases, contexts and migrations (#19) | @CarterBeasse | approved (JimRashed) | 2026-11-04 |
| #79 | feat: add shared React frontend foundation (#22) | @JimRashed | approved (CarterBeasse) | 2026-11-04 |
| #81 | feat(statistics): persist match results with idempotent recording (#33) | @CarterBeasse | commented (SuSu-Lisu) | 2026-11-04 |
| #80 | feat(identity): persist player profiles with race-safe provisioning (#27 | @CarterBeasse | commented (SuSu-Lisu) | 2026-11-04 |
| #75 | feat(gameplay): add match aggregate and round rules (#50) | @SuSu-Lisu | approved (CarterBeasse, JimRashed) | 2026-11-04 |
| #74 | feat(notification): add domain models and replay rules (#56) | @SuSu-Lisu | approved (CarterBeasse, JimRashed) | 2026-11-04 |
| #84 | feat(identity): add profile DTOs and application service (#28) | @CarterBeasse | approved (JimRashed) | 2026-11-04 |
| #87 | ci: run backend, architecture, security and frontend gates (#25) | @SuSu-Lisu | approved (CarterBeasse) | 2026-11-04 |
| #83 | test: add shared bearer-token test fixture (#24) | @SuSu-Lisu | approved (CarterBeasse, JimRashed) | 2026-11-04 |
| #82 | feat(notification): add EF mappings and repositories (#57) | @SuSu-Lisu | approved (JimRashed, CarterBeasse) | 2026-11-04 |
| #86 | feat(gameplay): persist matches with snapshots and answers (#51) | @SuSu-Lisu | approved (CarterBeasse) | 2026-11-04 |
| #85 | feat(statistics): add result/stats DTOs and application service (#34) | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-04 |
| #88 | feat(identity): expose /api/users/me and retire password routes (#29) | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-04 |
| #91 | feat(questionbank): add domain rules (#38) | @JimRashed | none | 2026-11-04 |
| #89 | feat(lobby): add owned service and React workflow (#12) | @JimRashed | approved (CarterBeasse, SuSu-Lisu) | 2026-11-04 |
| #93 | feat(statistics): expose results, stats, history and leaderboard routes  | @CarterBeasse | commented (SuSu-Lisu) | 2026-11-04 |
| #92 | feat(questionbank): add owned catalog persistence (#39) | @JimRashed | commented (SuSu-Lisu) | 2026-11-04 |
| #98 | feat: QuestionBank API contracts and application orchestration (#40) | @JimRashed | approved (CarterBeasse) | 2026-11-04 |
| #103 | feat: expose secured QuestionBank REST endpoints (#41) | @JimRashed | approved (CarterBeasse) | 2026-11-04 |
| #90 | feat(notification): DTOs, application service and secured endpoints (#58 | @SuSu-Lisu | approved (JimRashed) | 2026-11-04 |
| #94 | feat(client): notification events and preferences screens (#60) | @SuSu-Lisu | approved (CarterBeasse, JimRashed) | 2026-11-04 |
| #99 | test(client): notification slice coverage (#61) | @SuSu-Lisu | approved (CarterBeasse) | 2026-11-04 |
| #102 | feat(client): Identity profile, statistics and leaderboard screens (#30, | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-04 |
| #104 | test: Identity and Statistics vertical-slice security coverage (#31, #37 | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-04 |
| #106 | feat(client): add QuestionBank preview screen (#42) | @JimRashed | approved (CarterBeasse) | 2026-11-04 |
| #107 | fix(gameplay): recheck host on repeated start, absorb start and answer r | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-04 |
| #108 | fix(gameplay): map MatchConcurrencyException to 409 instead of 500 (#53) | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-04 |
| #109 | fix(client): make the match page reachable and playable by both players  | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-04 |
| #110 | docs: Milestone 3 architecture, security and reflection report (#65) | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-04 |
| #95 | feat(gameplay): API contracts and match application service (#52, part 1 | @SuSu-Lisu | commented (CarterBeasse) | 2026-11-04 |
| #96 | feat(gameplay): match endpoints and error mapping (#53) | @SuSu-Lisu | approved (JimRashed) | 2026-11-04 |
| #111 | Gameplay: typed service adapters and delivery outbox (#52 part 2) | @SuSu-Lisu | none | 2026-11-05 |
| #100 | feat(client): gameplay match page (#54) | @SuSu-Lisu | commented (CarterBeasse) | 2026-11-05 |
| #112 | test(lobby): audit authorization and multiplayer coverage (#49) | @JimRashed | approved (SuSu-Lisu) | 2026-11-05 |
| #113 | feat: start/stop scripts and local runbook for the six services (#62) | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-05 |
| #114 | Milestone 3: Layered Service Development | @SuSu-Lisu | commented (CarterBeasse) | 2026-11-05 |
| #116 | test(questionbank): cover SQL-backed routes and principal isolation (#43 | @JimRashed | approved (CarterBeasse) | 2026-11-05 |
| #117 | test(gameplay): cover the real match routes and the atomic outbox commit | @SuSu-Lisu | approved (CarterBeasse) | 2026-11-05 |
| #120 | Milestone 3 follow-up: gameplay and questionbank coverage (#55, #43) | @SuSu-Lisu | commented (CarterBeasse) | 2026-11-05 |
| #118 | fix(gameplay): machine token never reached Keycloak or the receivers (#5 | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-05 |
| #115 | feat: legacy data cutover tool and retire the aggregate host (#63) | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-05 |
| #121 | fix(client): guest can open the match from events + D03 end-to-end journ | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-05 |
| #119 | docs: requirements traceability map and issue export (#66) | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-05 |
| #122 | docs: Milestone 3 submission bundle (#69) | @CarterBeasse | approved (SuSu-Lisu) | 2026-11-05 |
| #123 | Milestone 3 follow-up: machine-token fix, D03 journeys, cutover, traceab | @SuSu-Lisu | none | 2026-11-05 |
## What the gates actually caught

Review was not ceremonial; these are changes that stopped being wrong because somebody looked:

- **#107 / #108** — the answer-race fix retried a conflicting save, but without the 409 mapping an
  exhausted retry would still surface as a 500. Review asked for the two to land together.
- **#109** — the diff carried ~6k lines of already-merged services because the branch was cut before
  those merges. Review asked for the retarget, so the human-gate record describes the change that
  was actually reviewed.
- **#117** — CI, not a human, caught that the new answer tests fetched `next-question` with a
  write-scoped token (403 by design). It was fixed (`8fcf796`) rather than waived, and the failure
  is recorded in the #55 gate log.
- **#118** — a live two-player run, not a review, exposed that match creation returned 503: the
  options bound the wrong configuration keys *and* the OAuth response was read as camelCase. Both
  are now pinned by unit tests.
- **#121** — merging it after #118 conflicted in `runbook.md`; the conflict was resolved by keeping
  both sets of evidence rows, not by dropping one.

## Gaps, recorded rather than hidden

- **No review at all:** #73, #76, #77, #91 (early layer PRs, merged same-day), #111 and #123 (mine).
  #111 is the largest unreviewed change in the milestone and is also where the machine-token defect
  came from — the two facts are related.
- **Comment only, no approval:** #70 (Carter dismissed), #80, #81, #92, #93, #95, #100, #114, #120.
- **The milestone merges into `main`** (#114, #120, #123) were merged on a comment review from
  CarterBeasse, not an approval. The review on #114 asked to wait for D02, D03, D05 and D06; the
  review on #120 asked to wait for #118. Both were merged before those landed, and the later PRs
  closed the gap.

## Recommendation for the last merge

Before the final milestone PR into `main` is treated as signed off:

1. #111 should get a retrospective read by a second pair of eyes, since it is the one substantive
   change nobody reviewed and it produced a production-shaped defect.
2. The three milestone merges should carry an explicit approval, not a comment, so the submission
   shows a gate that was passed rather than a gate that was noted.