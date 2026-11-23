# Milestone 4 — requirements traceability

Issue [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148) (SEC-D3), parent
[#136](https://github.com/JimRashed/Web-3-Final-Project/issues/136) (SEC-D), tracker
[#132](https://github.com/JimRashed/Web-3-Final-Project/issues/132) (QA-SEC).

- **Candidate revision:** `46f847b` on `feature/milestone-4-security-and-cross-device-profiles`
  (merge of [PR #160](https://github.com/JimRashed/Web-3-Final-Project/pull/160)).
- **Data source:** `gh pr view` / `gh api .../reviews` on 2026-11-23, collected in the Dispatcher Prime brief
  and refreshed for PR #151 and PR #160 the same day. Hand-built; `scripts/traceability/build-traceability.py`
  is specific to Milestone 3.
- **Issue links:** GitHub's `closingIssuesReferences` is empty for these PRs because the base branch is not
  the default branch. The issue for each PR comes from the closing reference in its PR body.
- **Machine-readable copy:** [traceability.json](traceability.json).
- This is agent reconciliation, not an approval.

## Issue → PR → commit → review

Times are UTC on 2026-11-22 or 2026-11-23. "Teammate approval before merge" means an `APPROVED` review from
someone other than the author, submitted before the merge.

| Issue | PR | Author | Merged by / at | Head → merge | Reviews (state @ commit) | Teammate approval before merge | Unreplied threads |
|---|---|---|---|---|---|---|---|
| [#137](https://github.com/JimRashed/Web-3-Final-Project/issues/137) | [#149](https://github.com/JimRashed/Web-3-Final-Project/pull/149) | CarterBeasse | CarterBeasse, 11-22 05:09 | `f2c5b0c` → `fb60334` | JimRashed APPROVED @ `96fd09a`, APPROVED @ `f2c5b0c` | Yes (head) | 0 |
| [#140](https://github.com/JimRashed/Web-3-Final-Project/issues/140) | [#152](https://github.com/JimRashed/Web-3-Final-Project/pull/152) | JimRashed | JimRashed, 11-22 18:14 | `a368a1a` → `b37b8c1` | CarterBeasse COMMENTED @ `a87ef1f`, APPROVED @ head | Yes | 2 |
| [#141](https://github.com/JimRashed/Web-3-Final-Project/issues/141) | [#155](https://github.com/JimRashed/Web-3-Final-Project/pull/155) | JimRashed | CarterBeasse, 11-22 20:06 | `c94da40` → `deef7c0` | CarterBeasse APPROVED @ head | Yes | 2 |
| [#139](https://github.com/JimRashed/Web-3-Final-Project/issues/139) | [#156](https://github.com/JimRashed/Web-3-Final-Project/pull/156) | JimRashed | CarterBeasse, 11-22 21:54 | `45dbbb1` → `7a45643` | CarterBeasse COMMENTED, APPROVED @ head | Yes | 4 |
| [#138](https://github.com/JimRashed/Web-3-Final-Project/issues/138) | [#153](https://github.com/JimRashed/Web-3-Final-Project/pull/153) | CarterBeasse | JimRashed, 11-22 22:49 | `15731b2` → `4cf75a5` | JimRashed COMMENTED, APPROVED @ head | Yes | 0 |
| [#143](https://github.com/JimRashed/Web-3-Final-Project/issues/143) | [#154](https://github.com/JimRashed/Web-3-Final-Project/pull/154) | CarterBeasse | SuSu-Lisu, 11-23 02:05 | `d7a1848` → `312271e` | JimRashed COMMENTED ×2, SuSu-Lisu APPROVED @ head | Yes | 0 |
| [#146](https://github.com/JimRashed/Web-3-Final-Project/issues/146) | [#157](https://github.com/JimRashed/Web-3-Final-Project/pull/157) | SuSu-Lisu | SuSu-Lisu, 11-23 02:12 | `6083ca5` → `a35d4f6` | none | No | 0 |
| [#142](https://github.com/JimRashed/Web-3-Final-Project/issues/142) | [#158](https://github.com/JimRashed/Web-3-Final-Project/pull/158) | SuSu-Lisu | SuSu-Lisu, 11-23 02:20:40 | `61d5ba3` → `e7bc675` | CarterBeasse CHANGES_REQUESTED 02:31 (after merge) | No | 2 |
| part of [#136](https://github.com/JimRashed/Web-3-Final-Project/issues/136) (docs) | [#161](https://github.com/JimRashed/Web-3-Final-Project/pull/161) | SuSu-Lisu | SuSu-Lisu, 11-23 03:00 | `3925530` → `5e26d5d` | none | No | 0 |
| [#143](https://github.com/JimRashed/Web-3-Final-Project/issues/143) follow-up | [#162](https://github.com/JimRashed/Web-3-Final-Project/pull/162) | CarterBeasse | SuSu-Lisu, 11-23 04:04 | `9cc8ee6` → `1dd14d9` | SuSu-Lisu APPROVED @ head | Yes | 0 |
| [#144](https://github.com/JimRashed/Web-3-Final-Project/issues/144) | [#159](https://github.com/JimRashed/Web-3-Final-Project/pull/159) | CarterBeasse | CarterBeasse, 11-23 04:34 | `63213ec` → `bc5a71e` | JimRashed, SuSu-Lisu COMMENTED; SuSu-Lisu APPROVED @ `9d82b99` | Yes, on an earlier commit | 0 |
| part of [#145](https://github.com/JimRashed/Web-3-Final-Project/issues/145) | [#151](https://github.com/JimRashed/Web-3-Final-Project/pull/151) | SuSu-Lisu | SuSu-Lisu, 11-23 04:59:24 | `a83b7a0` → `0bfbe11` | CarterBeasse CHANGES_REQUESTED @ `4c4ad3f` (11-22 17:58), COMMENTED @ `1ff2345` (02:53), COMMENTED @ `a83b7a0` (05:01:25, after merge) | No | 5 |
| part of [#147](https://github.com/JimRashed/Web-3-Final-Project/issues/147) | [#160](https://github.com/JimRashed/Web-3-Final-Project/pull/160) | SuSu-Lisu | SuSu-Lisu, 11-23 05:04:57 | `652ea23` → `46f847b` | CarterBeasse COMMENTED @ `652ea23` (05:22:27, after merge) | No | 0 |

All review threads show `resolved=false`: the team signals resolution by replying in the thread, not by
pressing "Resolve". Unreplied counts are threads with no reply from the PR author.

## Reviewer feedback and resolution

Only what the review record shows. "Unreplied" means the PR author has not replied in the thread.

| PR | Feedback raised | Where it stands |
|---|---|---|
| [#149](https://github.com/JimRashed/Web-3-Final-Project/pull/149) | JimRashed reviewed twice (`96fd09a`, `f2c5b0c`). | Both reviews are APPROVED; no open threads. The decisions document itself still records team approval as pending (see [human-gates.md](human-gates.md#security-decisions-approval-state)). |
| [#152](https://github.com/JimRashed/Web-3-Final-Project/pull/152) | CarterBeasse COMMENTED at `a87ef1f`, then APPROVED at head. | Approved; 2 threads unreplied. |
| [#155](https://github.com/JimRashed/Web-3-Final-Project/pull/155) | CarterBeasse review threads. | Approved at head; 2 threads unreplied. |
| [#156](https://github.com/JimRashed/Web-3-Final-Project/pull/156) | CarterBeasse COMMENTED, then APPROVED at head. | Approved; 4 threads unreplied. |
| [#153](https://github.com/JimRashed/Web-3-Final-Project/pull/153) | JimRashed COMMENTED, then APPROVED at head. | Approved; deferrals recorded under [open findings](#open-findings-on-46f847b) (SEC-10 deployment secret store). |
| [#154](https://github.com/JimRashed/Web-3-Final-Project/pull/154) | JimRashed COMMENTED twice; SuSu-Lisu APPROVED at head. | Approved; follow-up delivered in [#162](https://github.com/JimRashed/Web-3-Final-Project/pull/162) (CHECK constraints). |
| [#158](https://github.com/JimRashed/Web-3-Final-Project/pull/158) | CarterBeasse CHANGES_REQUESTED at 02:31, 11 minutes after the merge: security block on protected operations unchecked, controller binding / `[AllowAnonymous]` unchecked, provider-only note and CI link missing. | 2 threads unreplied; items open on `46f847b`. |
| [#159](https://github.com/JimRashed/Web-3-Final-Project/pull/159) | JimRashed and SuSu-Lisu COMMENTED; SuSu-Lisu APPROVED at `9d82b99`. | Three later review-fix commits were pushed after that approval and merged without a new approval. Low findings remain (see below). |
| [#151](https://github.com/JimRashed/Web-3-Final-Project/pull/151) | CarterBeasse CHANGES_REQUESTED at `4c4ad3f`, COMMENTED at `1ff2345`, and COMMENTED at head `a83b7a0` at 05:01:25, two minutes after the merge at 04:59:24. | 5 threads unreplied; findings open on `46f847b`. |
| [#160](https://github.com/JimRashed/Web-3-Final-Project/pull/160) | Post-merge review by CarterBeasse at 05:22:27 ([review 6016902334](https://github.com/JimRashed/Web-3-Final-Project/pull/160#pullrequestreview-6016902334)): three High, two Medium and four smaller findings. | No reply yet; findings open on `46f847b` and block closing #147. |
| [#157](https://github.com/JimRashed/Web-3-Final-Project/pull/157), [#161](https://github.com/JimRashed/Web-3-Final-Project/pull/161), [#162](https://github.com/JimRashed/Web-3-Final-Project/pull/162) | No review threads. | — |

## Review-compliance exceptions

Facts only: who merged, when, and the review state at that moment.

1. **#151** was merged by its author SuSu-Lisu at 04:59:24 with CarterBeasse's CHANGES_REQUESTED (at
   `4c4ad3f`) still the latest blocking state and no approval. CarterBeasse's next review comment on the head
   commit was submitted at 05:01:25. 5 threads are unreplied.
2. **#158** was merged by its author SuSu-Lisu at 02:20:40 before any review. CarterBeasse submitted
   CHANGES_REQUESTED at 02:31. 2 threads are unreplied.
3. **#157** (implementation, #146), **#160** (implementation, #147) and **#161** (docs, #136) were merged by
   their author SuSu-Lisu with no review. #160 received a post-merge review at 05:22:27.
4. **#159** was approved by SuSu-Lisu at `9d82b99`; three later commits (review fixes) were merged by the
   author CarterBeasse without a new approval.

## Open findings on `46f847b`

None of these is fixed by this PR (docs only). Proposed follow-ups are suggestions; Carter or the Dispatcher
decide whether to open issues.

| Source | Severity | Finding | Proposed follow-up |
|---|---|---|---|
| [#160 review](https://github.com/JimRashed/Web-3-Final-Project/pull/160#pullrequestreview-6016902334) | High | The journey cannot pass on a fresh stack: no profiles are created, and #144 never creates one implicitly, so a picture PUT without a profile returns 404. | Fix PR on the milestone branch that seeds or creates profiles before the picture steps. |
| #160 review | High | SEC-09 sign-out across devices is neither automated (A signs out, B keeps working) nor documented as manual Keycloak steps with expected outcomes. The #151 component test uses a mocked session. | Automate A-only sign-out in the journey, or document the manual steps in [validation-results.md](validation-results.md#147-cross-device-journey). |
| #160 review | High | No run evidence: the PR merged with no PASS/FAIL output and no review. | Paste a PASS/FAIL run on a fresh stack (Phase B). |
| #160 review | Medium | Step "B retrieves the new reference" is not checked: B never reads `GET /api/users/me` to compare `picture.version` and `contentType` with A's PUT. | Add the assertion in the same fix PR. |
| #160 review | Medium | The mismatched-magic case accepts 400 or 415; the merged contract and server return exactly 400. | Assert 400 only. |
| #160 review | Low | Smaller: a 404 check also accepts 200; third-account checks fall back to a hash compare and do not assert A's `version`/`updatedAt` unchanged; `digest(undefined)` throws on a non-image response and aborts the remaining checks (response `Content-Type` never asserted); the token is captured asynchronously before the context closes, which can race. | Same fix PR, as time allows. |
| [#151](https://github.com/JimRashed/Web-3-Final-Project/pull/151) | High | 413/415 responses show a generic "Try again" message. | Follow-up issue for #151 client error handling. |
| #151 | High | A failed upload hides the current picture. | Same follow-up. |
| #151 | Medium | 409 does not refetch the profile; the sign-out test has no "no GET after sign-out" assertion; replace-time object-URL revoke is not tested. | Same follow-up. |
| #151 | Low | `aria` label-in-name; "2 MB" wording; duplicate `Content-Type`; `readPicture` accepts a missing picture. | Same follow-up. |
| [#158](https://github.com/JimRashed/Web-3-Final-Project/pull/158) | Not graded | Security matrix does not check the security block on protected operations or controller binding / `[AllowAnonymous]`; provider-only note and CI link missing. | Follow-up issue for #158 matrix gaps. |
| [#159](https://github.com/JimRashed/Web-3-Final-Project/pull/159) | Low | Kestrel `BadHttpRequestException` maps to 500 on picture routes; duplicated `PictureSamples`; the `PUT /me` picture snapshot may be one version old. | Follow-up issue for #159 lows. |
| [#153](https://github.com/JimRashed/Web-3-Final-Project/pull/153) / [#154](https://github.com/JimRashed/Web-3-Final-Project/pull/154) | Deferred | Deployment secret store undecided (SEC-10, R7). | Team decision; recorded as a residual in the [threat model](../milestone3/threat-model.md#residual-risks-and-design-decisions). |
| [#137](https://github.com/JimRashed/Web-3-Final-Project/issues/137) | Deferred | Security-decision approval record incomplete: SuSu-Lisu's approval is pending. | See [human-gates.md](human-gates.md#what-is-still-needed-from-humans). |

## Issue state

| Issue | State | Evidence comment linked | Waits for |
|---|---|---|---|
| [#137](https://github.com/JimRashed/Web-3-Final-Project/issues/137), [#142](https://github.com/JimRashed/Web-3-Final-Project/issues/142), [#143](https://github.com/JimRashed/Web-3-Final-Project/issues/143), [#144](https://github.com/JimRashed/Web-3-Final-Project/issues/144), [#146](https://github.com/JimRashed/Web-3-Final-Project/issues/146) | Closed | Yes | — |
| [#133](https://github.com/JimRashed/Web-3-Final-Project/issues/133), [#134](https://github.com/JimRashed/Web-3-Final-Project/issues/134) (parents), [#138](https://github.com/JimRashed/Web-3-Final-Project/issues/138), [#139](https://github.com/JimRashed/Web-3-Final-Project/issues/139), [#140](https://github.com/JimRashed/Web-3-Final-Project/issues/140), [#141](https://github.com/JimRashed/Web-3-Final-Project/issues/141) | Closed | No | Evidence comment proposed; drafts in [submission.md](submission.md#proposed-evidence-comments). |
| [#145](https://github.com/JimRashed/Web-3-Final-Project/issues/145) | Open | — | UI merged in #151; live four-step check and screenshots not yet run; #151 findings above. |
| [#147](https://github.com/JimRashed/Web-3-Final-Project/issues/147) | Open | — | Journey merged in #160 but not run; the three High and two Medium #160 review findings above must be fixed, then a PASS/FAIL run on a fresh stack. |
| [#135](https://github.com/JimRashed/Web-3-Final-Project/issues/135) (SEC-C) | Open | — | #145. |
| [#136](https://github.com/JimRashed/Web-3-Final-Project/issues/136) (SEC-D) | Open | — | #147, #148. |
| [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148) (SEC-D3) | Open | — | Phase B results in [validation-results.md](validation-results.md). |
| [#132](https://github.com/JimRashed/Web-3-Final-Project/issues/132) (tracker) | Open | — | All of the above. |
