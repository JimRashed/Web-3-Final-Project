# Milestone 4 submission bundle (SEC-D3)

Issue [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148), parent
[#136](https://github.com/JimRashed/Web-3-Final-Project/issues/136), tracker
[#132](https://github.com/JimRashed/Web-3-Final-Project/issues/132). Final revision `main` at `14ac63e` (earlier candidate `68b7ecd`, first reconciled on `46f847b`).

This is the evidence index for Milestone 4. The milestone PR [#166](https://github.com/JimRashed/Web-3-Final-Project/pull/166)
(`feature/milestone-4-security-and-cross-device-profiles` → `main`, opened by SuSu-Lisu) was merged by
SuSu-Lisu on 2026-11-24 at 00:26:12, at head `6fa8b3e` (merge `14ac63e`), with CarterBeasse's
CHANGES_REQUESTED of 2026-11-23 12:36:23 (at `68b7ecd`) outstanding and JimRashed's review COMMENTED. Its
description's review record contradicts the reviews API (it lists #157, #158, #163, #164 and #165 as approved)
and it overstates approved contracts, matrix enforcement, rejected-upload behaviour and sign-out; it was not
corrected before the merge. CarterBeasse posted a [correcting comment](https://github.com/JimRashed/Web-3-Final-Project/pull/166#issuecomment-7118738500).
The facts to use are in [human-gates.md](human-gates.md) and [requirements-traceability.md](requirements-traceability.md).

## Evidence links

| Area | Document |
|---|---|
| Security decisions (SEC-01 to SEC-12) | [security-decisions.md](../milestone3/security-decisions.md) |
| Authorization matrix | [authorization-matrix.md](../milestone3/authorization-matrix.md) |
| Configuration review (SEC-10) | [configuration-review.md](../milestone3/configuration-review.md) |
| Identity contract | [identity.yaml](../../contracts/openapi/identity.yaml) |
| Threat model and mitigation trace | [threat-model.md](../milestone3/threat-model.md) |
| Traceability | [requirements-traceability.md](requirements-traceability.md) · [traceability.json](traceability.json) |
| Human gates | [human-gates.md](human-gates.md) |
| Validation results | [validation-results.md](validation-results.md) · #145 screenshots in [evidence/](evidence/145-README.md) |
| Plans | [136](../../specs/milestone4/136-plan.md) · [142](../../specs/milestone4/142-plan.md) · [145](../../specs/milestone4/145-plan.md) · [146](../../specs/milestone4/146-plan.md) · [147](../../specs/milestone4/147-plan.md) · [137](../../specs/issue-137-plan.md) · [138](../../specs/issue-138-plan.md) · [139](../../specs/issue-139-plan.md) · [140](../../specs/issue-140-plan.md) · [141](../../specs/issue-141-plan.md) · [143](../../specs/issue-143-plan.md) · [144](../../specs/issue-144-plan.md) · [148](../../specs/issue-148-plan.md) |

## Board

[Project 4](https://github.com/users/JimRashed/projects/4). Screenshot instructions: a table or board view
with the Owner, Status and dependency fields visible, filtered to the SEC issues (#132 to #148).

On 2026-11-23 (before #145 and #147 closed at 05:29) the board showed #132, #135, #136 and #148 in Backlog and
#145 and #147 In progress, which no longer matches the work. CarterBeasse cannot edit board fields, so JimRashed or SuSu-Lisu should update them
before the screenshot. Screenshot: not taken yet.

The #148 issue body still names JimRashed as assignee; the handover to CarterBeasse is recorded in the issue
comments.

## Closure checklist

Each item closes only with the named evidence linked in a comment. #148 closes last before the tracker. The
GitHub milestone stays open until CarterBeasse says otherwise.

| Order | Issue | Owner | Evidence required | State |
|---|---|---|---|---|
| 1 | [#145](https://github.com/JimRashed/Web-3-Final-Project/issues/145) | SuSu-Lisu | Screenshots ([validation-results.md](validation-results.md#145-manual-check)); response to #151 findings | **Closed** 05:29:25, [comment](https://github.com/JimRashed/Web-3-Final-Project/issues/145#issuecomment-7092450934) links #165 screenshots. Gaps: #165 merged with changes requested; headers, reload and sign-out cleanup not evidenced; #151 findings open |
| 2 | [#147](https://github.com/JimRashed/Web-3-Final-Project/issues/147) | SuSu-Lisu | Fix PR for the #160 findings, `journey:picture` PASS and SEC-09 sign-out evidence ([validation-results.md](validation-results.md#147-cross-device-journey)) | **Closed** 05:29:23, [comment](https://github.com/JimRashed/Web-3-Final-Project/issues/147#issuecomment-7092450545) pastes 17/17 PASS on `46f847b` + #163. Gaps: SEC-09 two-device sign-out not covered; other #160/#163 findings open |
| 3 | [#135](https://github.com/JimRashed/Web-3-Final-Project/issues/135) | CarterBeasse | #143, #144, #145 closed with links | **Closed** 11-23 23:00:29 by CarterBeasse, [evidence comment](https://github.com/JimRashed/Web-3-Final-Project/issues/135#issuecomment-7116442844) |
| 4 | [#136](https://github.com/JimRashed/Web-3-Final-Project/issues/136) | SuSu-Lisu | #146, #147, #148 closed with links | **Closed** 11-24 00:18:40 by SuSu-Lisu, [comment](https://github.com/JimRashed/Web-3-Final-Project/issues/136#issuecomment-7117932854) lists #148 as accepted while #148 was still open |
| 5 | [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148) | CarterBeasse | Results on the final revision `14ac63e` recorded; the items [below](#what-is-left-to-close-148) settled | Open (last before the tracker) |
| 6 | [#132](https://github.com/JimRashed/Web-3-Final-Project/issues/132) | JimRashed | All SEC parents closed; milestone evidence complete | Open; waits for #148 |
| — | #133, #134, #138, #139, #140, #141 | — | Evidence comments | **Posted** by CarterBeasse: [#133](https://github.com/JimRashed/Web-3-Final-Project/issues/133#issuecomment-7116436261), [#134](https://github.com/JimRashed/Web-3-Final-Project/issues/134#issuecomment-7116436684), [#138](https://github.com/JimRashed/Web-3-Final-Project/issues/138#issuecomment-7116437060), [#139](https://github.com/JimRashed/Web-3-Final-Project/issues/139#issuecomment-7116437405), [#140](https://github.com/JimRashed/Web-3-Final-Project/issues/140#issuecomment-7116437864), [#141](https://github.com/JimRashed/Web-3-Final-Project/issues/141#issuecomment-7116438288) |

### What is left to close #148

- **Live checks:** decide whether to run `journey:api`, `journey:ui`, `journey:picture`, the #145 browser
  check, SEC-09 two-device sign-out and the `TightenProfilePictureContentTypeCheck` migration against a running
  database on `14ac63e`, or to keep them recorded as not run ([validation-results.md](validation-results.md#final-revision-main-at-14ac63e)).
- **Board:** JimRashed or SuSu-Lisu update the [board](#board) fields and take the screenshot.
- **Security decisions:** SuSu-Lisu's approval of SEC-01 to SEC-12 is still pending ([human-gates.md](human-gates.md#security-decisions-approval-state)).
- **Unreplied threads:** #152 (2), #155 (2), #156 (4), #158 (2), #151 (5), #160 (4), #163 (1), #165 (4), #166 (3).
- **Retro-review:** a team decision on #157, #160, #161, #163, #165, #166 and the post-approval commits on #159
  ([human-gates.md](human-gates.md#what-is-still-needed-from-humans)).

## Milestone PR

[PR #166](https://github.com/JimRashed/Web-3-Final-Project/pull/166) is merged, so the earlier step of replacing its description from this file no
longer applies, and the draft body that was here has been removed. Its description was not corrected before
the merge; the [correcting comment](https://github.com/JimRashed/Web-3-Final-Project/pull/166#issuecomment-7118738500) and this bundle are the record.

## Proposed evidence comments

Drafted for the six issues that were closed without an evidence comment. CarterBeasse has since posted
evidence comments on all six (links in the [closure checklist](#closure-checklist)); the drafts are kept as
written. Test counts are as reported in the PR body at the time.

### #133 — SEC-A (parent)

> Evidence for SEC-A on `feature/milestone-4-security-and-cross-device-profiles`:
> - A1 #137 → PR #149 (merge `fb60334`), approved by JimRashed at `96fd09a` and `f2c5b0c`. The decisions
>   document still records team approval as pending (SuSu-Lisu).
> - A2 #138 → PR #153 (merge `4cf75a5`), approved by JimRashed at head.
> - A3 #139 → PR #156 (merge `7a45643`), approved by CarterBeasse at head.
> Traceability: `docs/milestone4/requirements-traceability.md`. Open item: SEC-10 deployment secret store
> undecided.

### #134 — SEC-B (parent)

> Evidence for SEC-B on `feature/milestone-4-security-and-cross-device-profiles`:
> - B1 #140 → PR #152 (merge `b37b8c1`), approved by CarterBeasse at head.
> - B2 #141 → PR #155 (merge `deef7c0`), approved by CarterBeasse at head.
> - B3 #142 → PR #158 (merge `e7bc675`), merged before review; CarterBeasse requested changes 11 minutes
>   after the merge and the matrix gaps are open.
> Traceability: `docs/milestone4/requirements-traceability.md`.

### #138 — A2 secrets and configuration

> Delivered by PR #153 (head `15731b2`, merge `4cf75a5`), approved by JimRashed at head. Validation reported
> in the PR: 1628 tests, Vitest 310, secret scan. Residual: deployment secret store undecided (SEC-10), see
> `docs/milestone3/configuration-review.md` and the threat model.

### #139 — A3 threat model

> Delivered by PR #156 (head `45dbbb1`, merge `7a45643`), approved by CarterBeasse at head; 4 review threads
> unreplied. Threat model: `docs/milestone3/threat-model.md`, reconciled with `46f847b` and `68b7ecd` by #148 (agent
> reconciliation; independent human review pending). Validation reported in the PR: 1594 tests.

### #140 — B1 token validation

> Delivered by PR #152 (head `a368a1a`, merge `b37b8c1`), approved by CarterBeasse at head; 2 review threads
> unreplied. Validation reported in the PR: 1542 tests. CI security matrix coverage: PR #158 (#142).

### #141 — B2 scope, actor and ownership checks

> Delivered by PR #155 (head `c94da40`, merge `deef7c0`), approved by CarterBeasse at head; 2 review threads
> unreplied. Validation reported in the PR: 1594 tests and the contract validator.
