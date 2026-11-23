# Issue #148 plan — [SEC][D3] Integrate verification and milestone PR evidence

> Issue: [#148](https://github.com/JimRashed/Web-3-Final-Project/issues/148) · Parent: #136 (SEC-D) · Tracker: #132 (QA-SEC)
> Owner: @CarterBeasse (handover from @JimRashed, recorded in the issue comments) · Suggested reviewer: @SuSu-Lisu
> Branch: `feat/148-milestone-evidence`, base `feature/milestone-4-security-and-cross-device-profiles`
> Candidate revision: **`46f847b`** (merge of PR #160; latest milestone-4 head at planning time)
> Prime brief: `.dispatcher/runs/148-202611230004/context.md`
> Plan status: **agent-reviewed** (Dispatcher pipeline). Not human-approved.

## 1. Summary and architecture

D3 is a delivery/review task. It produces evidence, not behavior: the milestone PR (milestone-4 → `main`)
needs one place that links the threat model, decisions, traceability, review record, validation results
and board state for one candidate revision.

The work is split in two phases because two prerequisites (#145 live check, #147 live journey) have merged
code but have never been run against a live stack:

- **Phase A (this PR, docs only).** Create the `docs/milestone4/` evidence set, reconcile
  `docs/milestone3/threat-model.md` with what is merged on `46f847b`, record review compliance factually,
  and prepare the validation-results skeleton with exact commands and `pending` result cells.
  No production code, no test code, no CI change.
- **Phase B (after live runs on the candidate).** Someone with the stack up runs the commands; results
  are filled in; owners close #145/#147; then #135/#136/#148 close with evidence links; the description of
  the existing draft milestone PR #166 is updated; it is never merged without Carter's explicit go.

Layer boundaries: only `docs/` and `specs/` change. Service code (`src/`), tests (`tests/`), contracts
(`contracts/`), scripts and `.github/workflows/` are read as evidence sources and are not modified.

Ground rules applied to every document written here:
- State review facts neutrally (who merged, when, review state at merge). No blame wording.
- Never record an approval that did not happen. Jim's approvals on PR #149 (`96fd09a`, `f2c5b0c`) are
  linkable evidence for #137; SuSu's approval of the security decisions is **pending**.
- Agent reconciliation is labelled as such ("agent reconciliation, not approval").
- No secret values, tokens, connection strings or passwords in any file.
- Historical test counts from PR bodies are labelled historical, never presented as current results.

## 2. File inventory

| Action | Path | Phase | Purpose |
|---|---|---|---|
| Create | `docs/milestone4/requirements-traceability.md` | A | Issue → PR → commit → review table, exceptions, open findings |
| Create | `docs/milestone4/traceability.json` | A | Machine-readable form of the same table |
| Create | `docs/milestone4/human-gates.md` | A | Per-PR independent-review status, exceptions, security-decision approval state |
| Create | `docs/milestone4/validation-results.md` | A (skeleton) / B (results) | Candidate-revision commands, CI link placeholders, #145 manual check, historical numbers |
| Create | `docs/milestone4/submission.md` | A (index) / B (final links) | Milestone PR evidence index, board instructions, closure checklist |
| Modify | `docs/milestone3/threat-model.md` | A (reconcile) / B (verified rows) | Status reconciliation against `46f847b` |
| Modify (pointer only) | `docs/milestone3/security-decisions.md` | A | Add an evidence link to Jim's PR #149 approvals in the Approval table's evidence column **without** changing either "Pending" status |
| Create | `specs/issue-148-plan.md` | A | This plan |

Not touched: `scripts/traceability/build-traceability.py` (it is hard-wired to the Milestone 3 milestone
and `docs/milestone3`; Phase A writes the M4 table by hand from the Prime brief, see Risks R4),
`docs/milestone3/requirements-traceability.md`, `docs/milestone3/traceability.json`,
`specs/implementation-plan.md`, everything under `src/`, `tests/`, `contracts/`, `.github/`.

## 3. Ordered steps — Phase A (this PR)

### A1. `docs/milestone4/requirements-traceability.md`
- Header: issue #148, parent #136, tracker #132, candidate `46f847b`, data source (Prime brief, `gh pr view`
  on 2026-11-23), note that GitHub `closingIssuesReferences` is empty because the base is not the default
  branch, so links come from the PR bodies' closing references.
- **Table 1 — issue → PR → commit → review**, one row per merged PR: #149/#137, #152/#140, #155/#141,
  #156/#139, #153/#138, #154/#143, #157/#146, #158/#142, #161/part of #136, #162/#143 follow-up,
  #159/#144, #151/part of #145, #160/part of #147. Columns: Issue, PR, Author, Merged by / at (UTC),
  Head → merge commit, Reviews (state @ commit), Teammate approval before merge (Yes / Yes on earlier
  commit / No), Unreplied review threads. Values copied exactly from the brief.
- Sentence under the table: all threads show `resolved=false` because the team signals resolution by
  replying, not by resolving.
- **Reviewer feedback and resolution** section: for each PR with review comments, one line on what was
  raised and where it was resolved (reply/commit) or that it is unreplied. Unreplied counts come from the
  brief (#152: 2, #155: 2, #156: 4, #158: 2, #151: 5).
- **Review-compliance exceptions** (neutral wording, facts only): #151 merged by its author with Carter's
  CHANGES_REQUESTED outstanding and no approval; #158 merged at 02:20:40 before any review, Carter's
  CHANGES_REQUESTED followed at 02:31; #157, #160 and #161 merged with no review; #159 approved at
  `9d82b99`, four later review-fix commits not re-approved.
- **Open findings on `46f847b`** with where each stands and the proposed follow-up (see §8): #151 client
  findings, #158 matrix gaps, #159 lows, #153/#154 deferrals (SEC-10 deployment secret store, R7; #137
  approval record incomplete).
- **Issue state** table: closed with evidence (#137, #142, #143, #144, #146); closed without evidence
  comment (#133, #134, #138, #139, #140, #141, with "evidence comment proposed, see submission.md");
  open (#132, #135, #136, #145, #147, #148) with what each waits for.
- Done check: every PR and issue number is a full GitHub link; every row in the brief appears once.

### A2. `docs/milestone4/traceability.json`
- Shape (mirrors the M3 export's spirit, smaller): `repository`, `milestone`
  ("Security and Cross-Device Profiles"), `candidateRevision: "46f847b"`, `generated` (date, "hand-built
  from Prime brief"), `pullRequests[]` {number, issues[], author, mergedBy, mergedAt, head, merge,
  reviews[] {reviewer, state, commit}, approvedBeforeMerge ("yes"|"earlier-commit"|"no"),
  unrepliedThreads}, `exceptions[]` {pr, kind, detail}, `openFindings[]` {source, severity, summary,
  followUp}, `issues[]` {number, state, evidenceLinked (bool), waitsFor[]}.
- Done check: `node -e "JSON.parse(require('fs').readFileSync('docs/milestone4/traceability.json','utf8'))"`
  succeeds; PR numbers and counts match Table 1.

### A3. `docs/milestone4/human-gates.md`
- Sections: "The gate this milestone asks for" (each implementation PR reviewed by a different member
  before merge; each owner supplies validation evidence), "Per-PR record" (reviewer, state, at which
  commit, before/after merge, owner validation evidence present: PR-body numbers or "CI only"),
  "Exceptions" (same list as A1, no adjectives), "Security decisions approval state" (SEC decisions in
  `docs/milestone3/security-decisions.md` remain **Proposed**; Jim's PR-level approvals of #149 at
  `96fd09a` and `f2c5b0c` linked as evidence of review; SuSu **pending**; no formal decision approval
  recorded), "Threat model review" (independent human review still pending; this PR's reconciliation is
  agent work), "What is still needed from humans" (SuSu decision approval, review of this PR, owners'
  replies on unreplied threads, retro-review of #157/#160/#161 if the team wants it).
- Done check: grep for "approved" shows only approvals present in Table 1 or the #149 links.

### A4. `docs/milestone4/validation-results.md` (skeleton)
- **Candidate revision** block: `46f847b`, branch, date run = pending, runner = pending, OS/tool versions
  = pending.
- **Commands** (exact, from repo root unless stated) with a Result column set to `pending`:
  1. `dotnet restore QuizArena.sln` then `dotnet build QuizArena.sln --no-restore -warnaserror`
  2. `dotnet test` per test project (13 rows: ArchitectureTests; Unit and Integration for Gameplay,
     Identity, Lobby, Notification, QuestionBank, Statistics), e.g.
     `dotnet test tests/QuizArena.Identity.IntegrationTests --no-build`; integration projects need the
     SQL Server from the local stack or CI.
  3. Client (`src/QuizArena.Client`): `npm ci`, `npm run typecheck`, `npm run lint`, `npx vitest run`,
     `npm run build`.
  4. Contracts: `node contracts/validate-contracts.mjs`.
  5. Live e2e: `./scripts/start-milestone3.ps1`; `cd tests/e2e; npm ci; npm run journey:api`,
     `npm run journey:ui`, `npm run journey:picture` (needs Microsoft Edge); `./scripts/stop-milestone3.ps1`.
- **CI** table: workflow `ci.yml` jobs backend (restore, build, SQL Server, `dotnet test`) and frontend
  (typecheck, lint, vitest, build); run on `46f847b` = run 45115561119 — link placeholder, conclusion
  `pending` until checked; note there is no e2e job in CI, so e2e is manual evidence.
- **#145 manual check** (four steps copied from the #145 issue comment, each with expected outcome and a
  `pending` result + screenshot slot: display, missing, refused type error, signed out).
- **#147 journey** expected outcome slot (`journey:picture`), result `pending`.
- **Historical per-PR numbers** table, explicitly labelled "reported in PR bodies at the time; not
  current evidence": #149 1456/1456; #152 1542; #155 1594 + validator; #156 1594; #153 1628 + Vitest 310 +
  secret scan; #154 rehearsal + CI; #159 1953/1953; #162 Identity 75/233/28; #157/#158/#160 CI only;
  #151 Vitest.
- Done check: no result cell claims a pass for `46f847b`; every command is copy-pasteable.

### A5. `docs/milestone4/submission.md` (evidence index)
- Draft of the milestone PR body following the M3 convention (#114): `## Milestone 4 — Security and
  Cross-Device Profiles`, `## Change summary`, `## Validation results`, `## Human review signoff`,
  `## Notes for the reviewer`.
- **Evidence links**: design (`docs/milestone3/security-decisions.md`, `authorization-matrix.md`,
  `configuration-review.md`, `contracts/openapi/identity.yaml`), threat model, traceability (md + json),
  human gates, validation results, plans (`specs/milestone4/*-plan.md`, `specs/issue-1xx-plan.md`).
- **Board**: Project 4 link; screenshot instructions (view with Owner, Status, dependency fields
  visible). State in the doc that Carter cannot edit board fields, the brief shows #132/#135/#136/#148 in
  Backlog and #145/#147 In progress, and Jim or SuSu should update fields before the screenshot. The
  screenshot itself is Phase B.
- **Closure checklist** (order matters, each line names the evidence link required): #145 (owner SuSu,
  needs four-step check), #147 (owner SuSu, needs `journey:picture` result) → #135 → #136 → #148 → #132.
  Plus "evidence comments not yet posted on #133, #134, #138, #139, #140, #141 (drafts below)" (see A7).
- Done check: every link resolves to a file in the branch or a GitHub URL.

### A6. Reconcile `docs/milestone3/threat-model.md`
Edit status text only; keep table structure. Mark the change as "Agent reconciliation for D3 (#148) on
`46f847b`, not an approval" in line 3 / line 69.
- Line 3: reviewed base `deef7c0` → candidate `46f847b`.
- Row 41: "verification pending #142" → "covered by CI tests from #158 (merged without approval); gaps recorded (security block on protected
  operations, controller binding/`[AllowAnonymous]` check, provider-only note and CI link)".
- Row 43: pending #142/#146 → covered by CI tests from #157 and #158 (merged without approval).
- Row 45: pending #146 → covered by CI tests from #157 (merged without approval).
- Rows 42/44/46: reconcile with #155/#153; keep SEC-10 deployment secret store as residual.
- Rows 47/48: add #162 CHECK constraints; replace "approval pending" with "decision approval pending
  (SuSu)", not "approved".
- Row 49: client part merged in #151 with open finding (failed upload hides current picture).
- Row 50 and line 35: "#151 sign-out tests and #160 journey merged; live verification pending".
- Line 58 residual: Identity picture paths and `/me` fixed by #143/#159 (DR-3); Statistics, Gameplay and
  Notification gaps remain (DR-8).
- Lines 65–67 verification table: add links to #157, #158, #151, #160.
- Line 69: record reviewers so far (Jim on #149; threat-model independent review pending) and point to
  `docs/milestone4/human-gates.md`.
- `docs/milestone3/security-decisions.md` Approval table: evidence cell for JimRashed links PR #149
  approvals (`96fd09a`, `f2c5b0c`) as "PR review approval; formal decision approval not recorded". Both
  statuses stay **Pending**.
- Done check: `git diff` shows only status/link text changes; no row says "approved" or "verified" for
  #145/#147 live behavior.

### A7. Evidence-comment drafts (Dispatcher posts after review — not done in this PR)
List in `submission.md` an appendix "Proposed evidence comments" with, per issue, the source lines:
#133 (parent: child issues + PRs from Table 1), #134 (parent: same), #138 → PR #153 + its validation
line, #139 → PR #156 + threat model link, #140 → PR #152 + validation line, #141 → PR #155 + validator
line. The plan stage and this PR do not write to GitHub.

### A8. Static validation (see §6) and hand-off to Review.

## 3b. Ordered steps — Phase B (after live runs on the candidate; not this PR's scope to execute)
- B1. A team member with the stack runs §6 Phase B commands on `68b7ecd` (or a later head; if the head
  moves, the candidate revision is updated everywhere in one commit and every result is rerun).
- B2. Fill `validation-results.md` result cells, CI run link and conclusion, screenshots for #145.
- B3. Threat model rows 49/50 and line 35 → "verified (link)", only with the result link.
- B4. Owners close #145 and #147 with evidence links; then #135, #136, #148 are closed with links.
- B5. Board screenshot after Jim/SuSu update fields; link it in `submission.md`.
- B6. Dispatcher updates the description of the existing draft milestone PR #166 (milestone-4 → `main`,
  opened by SuSu-Lisu) from `submission.md`; no new PR is opened.
  It is never merged without Carter's explicit go (memory rule: no milestone close without go).

## 4. Acceptance coverage

| # | Criterion | Steps | Phase |
|---|---|---|---|
| 1 | Reconcile the threat model and cross-device flow with the integrated implementation | A6 (status reconciliation, line 35 flow), B3 (verified rows) | A, finalised in B |
| 2 | Gather unit, integration, contract and e2e results for the same candidate revision, with commands, CI links, manual steps and expected outcomes | A4 (commands, expected outcomes, manual steps, CI placeholders), B1–B2 (results) | A skeleton, **results B** |
| 3 | Board view, design and mitigation docs, and verification results in the milestone PR evidence | A5 (index, board instructions), B5 (screenshot), B6 (draft PR) | A index, B screenshot + PR |
| 4 | Trace commits and PRs to issues and record reviewer feedback and resolution | A1, A2 | A |
| 5 | Verify each owner supplies validation evidence and each implementation PR has review from a different member before merge | A3 (per-PR record, exceptions stated), A1 exceptions | A (verification recorded; exceptions are findings, not fixed here) |
| 6 | Close items only when their acceptance evidence is linked | A5 closure checklist, A7 evidence-comment drafts, B4 | A preparation, closure B |

Criterion 5 note: the verification is done and recorded; it shows five PRs that did not meet the
before-merge review rule (#151, #157, #158, #160, #161) and one re-approval gap (#159). The plan records
these; remediation (retro-reviews) is a team decision listed in human-gates.md.

## 5. Testing strategy
Docs-only change: no new xUnit, Vitest or e2e tests. Phase A uses static checks (§6). The existing
suites are the evidence that Phase B runs; no test file is added or changed, and no dispatcher-specific
test folder is created.

## 6. Validation commands

Phase A (run by Implement/Validate on this branch; no live stack needed):
```bash
# JSON export parses
node -e "JSON.parse(require('fs').readFileSync('docs/milestone4/traceability.json','utf8'))"
# Relative links and anchors in changed docs resolve (small inline node script over
# docs/milestone4/*.md and docs/milestone3/{threat-model,security-decisions}.md: every ](path) exists,
# every #anchor matches a heading slug in the target file)
# No secret values (expect no matches other than prose about the secret store)
git diff origin/feature/milestone-4-security-and-cross-device-profiles -- docs specs | grep -inE "password=|secret=|client_secret|connectionstring|bearer [a-z0-9]" || true
# No fabricated approvals: review every hit by hand against Table 1 / PR #149
grep -rniE "approved|approval" docs/milestone4 docs/milestone3/threat-model.md
# Contracts still validate
node contracts/validate-contracts.mjs
# Build unaffected (docs only)
dotnet build QuizArena.sln --no-restore -warnaserror
```

Phase B (on the candidate revision, live stack; results go into validation-results.md):
```powershell
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet test tests/<each of the 13 test projects> --no-build
cd src/QuizArena.Client; npm ci; npm run typecheck; npm run lint; npx vitest run; npm run build; cd ../..
node contracts/validate-contracts.mjs
./scripts/start-milestone3.ps1
cd tests/e2e; npm ci; npm run journey:api; npm run journey:ui; npm run journey:picture; cd ../..
# #145 four-step manual check with screenshots
./scripts/stop-milestone3.ps1
```
Never copy historical counts as current results.

## 7. Risks and out of scope

Risks:
- **R1 Candidate drift.** If milestone-4 moves past `68b7ecd` before Phase B, all candidate references
  and results must move together; mixed revisions invalidate criterion 2.
- **R2 Approval wording.** Any phrasing implying SuSu approved decisions or that the threat model was
  independently reviewed would be false. Mitigated by the grep check and neutral templates.
- **R3 Sensitive tone.** Exceptions name teammates' merges; wording stays factual (who, when, state).
- **R4 Hand-built traceability.** `build-traceability.py` is M3-specific; the M4 table is copied from the
  Prime brief (gh data of 2026-11-23). Generalising the script is out of scope; a refresh before the
  milestone PR is a Phase B check.
- **R5 Board.** Carter cannot edit board fields; the screenshot depends on Jim/SuSu.
- **R6 CI run 45115561119** conclusion on `46f847b` was not confirmed at planning time; left `pending`.
- **R7 Issue ownership.** The issue body still names Jim as assignee; the handover comment is the
  record. Mentioned in submission.md.

Follow-up issues to propose (not created by this PR; Dispatcher/Carter decide):
- #151 unresolved client error-handling findings (413/415 generic message, failed upload hides current
  picture, 409 no refetch, missing tests for sign-out no-GET and replace revoke, aria, wording,
  duplicate Content-Type, `readPicture` accepting a missing picture).
- #158 security-matrix gaps (protected-op security block, binding/`[AllowAnonymous]` check,
  provider-only note, CI link).
- #159 lows (Kestrel `BadHttpRequestException` → 500 on picture routes, duplicated `PictureSamples`,
  `PUT /me` picture snapshot one version old).

Out of scope:
- Any production, test, contract, script or CI change, including fixes for the findings above.
- Running the live journeys or the #145 manual check (Phase B, a human with the stack).
- Posting GitHub comments, closing issues, editing the board, opening or merging any PR (Dispatcher /
  owners; milestone PR stays draft until Carter's go).
- Rewriting `docs/milestone3/*` beyond the threat-model reconciliation and the security-decisions
  evidence pointer.
