# Milestone 4 — human gate record

Issue [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148) (SEC-D3), parent
[#136](https://github.com/JimRashed/Web-3-Final-Project/issues/136). Final revision `main` at `14ac63e` (earlier candidate `68b7ecd`, first reconciled on `46f847b`).

This page records what the GitHub reviews API showed on 2026-11-23 and 2026-11-24 for every PR merged into
`feature/milestone-4-security-and-cross-device-profiles`, and for the milestone PR #166 into `main`. It was assembled by the Dispatcher pipeline: it is
agent reconciliation, not an approval, and it does not replace any human signoff.

## The gate this milestone asks for

| Gate | Question | Evidence |
|---|---|---|
| Owner verification | Did the owner supply validation evidence? | Commands and results in the PR body, or CI only; see [validation-results.md](validation-results.md) |
| Independent review | Did a team member other than the author approve before merge? | An `APPROVED` review by another member, submitted before the merge, on the merged commit |

A `COMMENTED` or `CHANGES_REQUESTED` review is not an approval.

## Per-PR record

| PR (issue) | Author | Reviewer, state @ commit | Before or after merge | Owner validation evidence in PR |
|---|---|---|---|---|
| [#149](https://github.com/JimRashed/Web-3-Final-Project/pull/149) (#137) | CarterBeasse | JimRashed APPROVED @ `96fd09a`, APPROVED @ `f2c5b0c` (head) | Before | 1456/1456 tests |
| [#152](https://github.com/JimRashed/Web-3-Final-Project/pull/152) (#140) | JimRashed | CarterBeasse COMMENTED @ `a87ef1f`, APPROVED @ head | Before | 1542 tests |
| [#155](https://github.com/JimRashed/Web-3-Final-Project/pull/155) (#141) | JimRashed | CarterBeasse APPROVED @ head | Before | 1594 tests + contract validator |
| [#156](https://github.com/JimRashed/Web-3-Final-Project/pull/156) (#139) | JimRashed | CarterBeasse COMMENTED, APPROVED @ head | Before | 1594 tests |
| [#153](https://github.com/JimRashed/Web-3-Final-Project/pull/153) (#138) | CarterBeasse | JimRashed COMMENTED, APPROVED @ head | Before | 1628 tests + Vitest 310 + secret scan |
| [#154](https://github.com/JimRashed/Web-3-Final-Project/pull/154) (#143) | CarterBeasse | JimRashed COMMENTED ×2; SuSu-Lisu APPROVED @ head | Before | Migration rehearsal + CI |
| [#157](https://github.com/JimRashed/Web-3-Final-Project/pull/157) (#146) | SuSu-Lisu | none | — | CI only |
| [#158](https://github.com/JimRashed/Web-3-Final-Project/pull/158) (#142) | SuSu-Lisu | CarterBeasse CHANGES_REQUESTED (02:31) | After (merged 02:20:40) | CI only |
| [#161](https://github.com/JimRashed/Web-3-Final-Project/pull/161) (#136, docs) | SuSu-Lisu | none | — | — (docs) |
| [#162](https://github.com/JimRashed/Web-3-Final-Project/pull/162) (#143 follow-up) | CarterBeasse | SuSu-Lisu APPROVED @ head | Before | Identity 75/233/28 |
| [#159](https://github.com/JimRashed/Web-3-Final-Project/pull/159) (#144) | CarterBeasse | JimRashed, SuSu-Lisu COMMENTED; SuSu-Lisu APPROVED @ `9d82b99` | Before, on an earlier commit | 1953/1953 tests |
| [#151](https://github.com/JimRashed/Web-3-Final-Project/pull/151) (#145) | SuSu-Lisu | CarterBeasse CHANGES_REQUESTED @ `4c4ad3f`; COMMENTED @ `1ff2345`; COMMENTED @ `a83b7a0` (05:01:25) | Last comment after merge (merged 04:59:24) | Vitest |
| [#160](https://github.com/JimRashed/Web-3-Final-Project/pull/160) (#147) | SuSu-Lisu | CarterBeasse COMMENTED @ `652ea23` (05:22:27) | After (merged 05:04:57) | CI only; no journey run output |
| [#163](https://github.com/JimRashed/Web-3-Final-Project/pull/163) (#147) | SuSu-Lisu | CarterBeasse COMMENTED @ `ae142e6` (05:29:22) | After (merged 05:29:19, 3 s earlier) | PR body reports 17/17 with 13 lines pasted; full run on the #147 issue |
| [#165](https://github.com/JimRashed/Web-3-Final-Project/pull/165) (#145, evidence docs) | SuSu-Lisu | CarterBeasse CHANGES_REQUESTED @ `7bfa9f1` (05:28:47) | Before, outstanding at merge (merged 05:29:21, 34 s later) | Screenshots of a live run on `46f847b` |
| [#167](https://github.com/JimRashed/Web-3-Final-Project/pull/167) (#162 follow-up) | CarterBeasse | JimRashed APPROVED @ `39cd133` (head, 23:24:11) | Before (merged 23:30:29) | Migration and store tests in the PR |
| [#164](https://github.com/JimRashed/Web-3-Final-Project/pull/164) (#148, Phase A docs) | CarterBeasse | JimRashed COMMENTED @ `67e1ebe`; APPROVED @ `d56f5ff` (head, 23:23:43) | Before (merged 23:30:33) | Automated results on `68b7ecd` (docs) |
| [#166](https://github.com/JimRashed/Web-3-Final-Project/pull/166) (milestone → `main`) | SuSu-Lisu | CarterBeasse CHANGES_REQUESTED @ `68b7ecd` (11-23 12:36:23); JimRashed COMMENTED @ `68b7ecd` | Outstanding at merge (merged 11-24 00:26:12 at `6fa8b3e`) | CI; description's review record contradicts the reviews API |

The test counts are what each PR body reported at the time. They are historical, not results for the candidate revision.

## Exceptions

Nine of eighteen PRs met the independent-review gate on their merged commit (including #164 and #167). The others:

1. **#151**: merged by the author with CarterBeasse's CHANGES_REQUESTED (at `4c4ad3f`) outstanding and no
   approval. CarterBeasse's comment on the head commit came at 05:01:25, two minutes after the merge at 04:59:24.
2. **#158**: merged by the author at 02:20:40 before any review; CarterBeasse's CHANGES_REQUESTED followed at 02:31.
3. **#157**, **#160** and **#161**: merged by the author with no review. #160 received a post-merge review at
   05:22:27 ([review 6016902334](https://github.com/JimRashed/Web-3-Final-Project/pull/160#pullrequestreview-6016902334)).
4. **#159**: approved at `9d82b99`; four later review-fix commits (`82f0d14`, `4c6907a`, `4b9def6`, `63213ec`)
   were merged without a new approval.
5. **#163**: merged by the author at 05:29:19 with no review; CarterBeasse's COMMENTED review followed at
   05:29:22 ([review 6016944462](https://github.com/JimRashed/Web-3-Final-Project/pull/163#pullrequestreview-6016944462)).
6. **#165**: merged by the author at 05:29:21 with CarterBeasse's CHANGES_REQUESTED of 05:28:47 outstanding
   and no approval ([review 6016941081](https://github.com/JimRashed/Web-3-Final-Project/pull/165#pullrequestreview-6016941081)).

7. **#166** (milestone PR → `main`): merged by the author SuSu-Lisu on 2026-11-24 at 00:26:12 (head `6fa8b3e`,
   merge `14ac63e`) with CarterBeasse's CHANGES_REQUESTED (at `68b7ecd`) outstanding, JimRashed COMMENTED and no
   approval. Its "Review record" section was not corrected and contradicts the reviews API; CarterBeasse posted a
   [correcting comment](https://github.com/JimRashed/Web-3-Final-Project/pull/166#issuecomment-7118738500).

Nine PRs in total: #151, #157, #158, #159, #160, #161, #163, #165 and #166.

Details and the open findings these reviews raised are in
[requirements-traceability.md](requirements-traceability.md#review-compliance-exceptions).

## Security decisions approval state

- [`docs/milestone3/security-decisions.md`](../milestone3/security-decisions.md) remains **Proposed — team
  approval pending**.
- JimRashed: approved [PR #149](https://github.com/JimRashed/Web-3-Final-Project/pull/149) at `96fd09a` and at
  `f2c5b0c`. That is a PR review approval and is linked as evidence in the decisions document's approval table;
  a formal decision approval is not recorded, so the status there stays **Pending**.
- SuSu-Lisu: **pending**. No review of #149 or of the decisions is recorded.
- CarterBeasse is the author and does not count as an approver.

## Threat model review

[`docs/milestone3/threat-model.md`](../milestone3/threat-model.md) was reconciled against `46f847b` and then
`68b7ecd` by #164, and its reviewed base updated to `14ac63e` afterwards. That reconciliation is agent work. Independent human review of the threat model is still pending.

## What is still needed from humans

- SuSu-Lisu's decision on the SEC-01 to SEC-12 decisions (approval or requested changes), recorded on #137 or #149.
- A human review of the threat-model reconciliation.
- Replies from PR authors on the unreplied threads: #152 (2), #155 (2), #156 (4), #158 (2), #151 (5), #160 (4),
  #163 (1), #165 (4) and #166 (3).
- A team decision on whether #157, #160, #161, #163, #165 and #166 get a retro-review, and whether the four
  post-approval commits on #159 need one.
- A team view on #145 and #147, which were closed at 05:29:25 and 05:29:23 with the gaps recorded in
  [requirements-traceability.md](requirements-traceability.md#issue-state).
- A response to CarterBeasse's CHANGES_REQUESTED review (2026-11-23 12:36:23, at `68b7ecd`) on milestone PR
  [#166](https://github.com/JimRashed/Web-3-Final-Project/pull/166), which was merged with it outstanding: its description's review record contradicts the
  reviews API (it lists #157, #158, #163, #164 and #165 as approved) and it overstates approved contracts, matrix
  enforcement, rejected-upload behaviour and sign-out. The record on this page is the one taken from the reviews
  API; see also the [correcting comment](https://github.com/JimRashed/Web-3-Final-Project/pull/166#issuecomment-7118738500).
- A decision on the live checks listed as not run in [validation-results.md](validation-results.md#final-revision-main-at-14ac63e):
  run them on `14ac63e`, or keep them recorded as not run.
