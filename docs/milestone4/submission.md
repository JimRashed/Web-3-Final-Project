# Milestone 4 submission bundle (SEC-D3)

Issue [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148), parent
[#136](https://github.com/JimRashed/Web-3-Final-Project/issues/136), tracker
[#132](https://github.com/JimRashed/Web-3-Final-Project/issues/132). Candidate revision `68b7ecd` (first reconciled on `46f847b`).

This is the evidence index for the milestone PR (`feature/milestone-4-security-and-cross-device-profiles` →
`main`). Phase A (this file) collects the links; Phase B adds the live results, the board screenshot and opens
the milestone PR as a **draft**. The milestone PR is not merged without Carter's explicit go.

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
before the screenshot. Screenshot: pending (Phase B).

The #148 issue body still names JimRashed as assignee; the handover to CarterBeasse is recorded in the issue
comments.

## Closure checklist

Close in this order. Each item closes only with the named evidence linked in a comment. #145 and #147 were
closed by SuSu-Lisu on 2026-11-23 with evidence linked and gaps recorded, so #135 and #136 can now be
considered; #148 closes last before the tracker.

| Order | Issue | Owner | Evidence required | State |
|---|---|---|---|---|
| 1 | [#145](https://github.com/JimRashed/Web-3-Final-Project/issues/145) | SuSu-Lisu | Screenshots ([validation-results.md](validation-results.md#145-manual-check)); response to #151 findings | **Closed** 05:29:25, [comment](https://github.com/JimRashed/Web-3-Final-Project/issues/145#issuecomment-7092450934) links #165 screenshots. Gaps: #165 merged with changes requested; headers, reload and sign-out cleanup not evidenced; #151 findings open |
| 2 | [#147](https://github.com/JimRashed/Web-3-Final-Project/issues/147) | SuSu-Lisu | Fix PR for the #160 findings, `journey:picture` PASS and SEC-09 sign-out evidence ([validation-results.md](validation-results.md#147-cross-device-journey)) | **Closed** 05:29:23, [comment](https://github.com/JimRashed/Web-3-Final-Project/issues/147#issuecomment-7092450545) pastes 17/17 PASS on `46f847b` + #163. Gaps: SEC-09 two-device sign-out not covered; other #160/#163 findings open |
| 3 | [#135](https://github.com/JimRashed/Web-3-Final-Project/issues/135) | CarterBeasse | #143, #144, #145 closed with links | Open; children closed, can be considered (gaps above) |
| 4 | [#136](https://github.com/JimRashed/Web-3-Final-Project/issues/136) | SuSu-Lisu | #146, #147, #148 closed with links | Open; #146 and #147 closed, waits for #148 |
| 5 | [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148) | CarterBeasse | Phase B results filled in on `68b7ecd`; this bundle linked from the draft milestone PR | Open (last before the tracker) |
| 6 | [#132](https://github.com/JimRashed/Web-3-Final-Project/issues/132) | JimRashed | All SEC parents closed; milestone PR evidence complete | Open |
| — | #133, #134, #138, #139, #140, #141 | — | Already closed; evidence comments posted (drafts [below](#proposed-evidence-comments)) | Drafted, not posted |

## Milestone PR body (draft)

Follows the Milestone 3 convention from [PR #114](https://github.com/JimRashed/Web-3-Final-Project/pull/114).
Phase B fills the `pending` parts; the PR is opened as a draft.

```markdown
## Milestone 4 — Security and Cross-Device Profiles

## Change summary
- SEC-A: security decisions, configuration and secrets, threat model (#137, #138, #139; PRs #149, #153, #156)
- SEC-B: token validation, scope/actor/ownership policies, CI security matrix (#140, #141, #142; PRs #152, #155, #158)
- SEC-C: private picture persistence, owner-only picture API, React profile picture UI (#143, #144, #145; PRs #154, #162, #159, #151, #165)
- SEC-D: Gameplay service-to-service verification, cross-device journey, evidence (#146, #147, #148; PRs #157, #160, #163, this bundle)
Evidence index: docs/milestone4/submission.md

## Validation results
Candidate revision: 68b7ecd. See docs/milestone4/validation-results.md.
CI on 68b7ecd: run 45118087469, success. Local suites, client checks, contract validator: pending.
Recorded on 46f847b: journey:picture 17/17 PASS (with #163, issue #147); #145 screenshots (PR #165).
SEC-09 two-device sign-out: not covered. Same-revision live runs on 68b7ecd: pending.

## Human review signoff
Per-PR review record and exceptions: docs/milestone4/human-gates.md.
Security decisions: JimRashed approved PR #149 (96fd09a, f2c5b0c); SuSu-Lisu pending.
Threat model independent review: pending.

## Notes for the reviewer
- Open findings on the candidate (PR #151, #158, #159, #160, #163, #165 reviews): docs/milestone4/requirements-traceability.md
- Eight PRs merged without a teammate approval on the merged commit: docs/milestone4/human-gates.md
- Board screenshot: pending.
- Not to be merged without Carter's explicit go.
```

## Proposed evidence comments

Drafts for the six issues that were closed without an evidence comment. The Dispatcher or the owner posts
them after this PR is reviewed; this PR does not write to GitHub. Test counts are as reported in the PR body
at the time.

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
