# Issue #6: Dispatcher Agent

## Summary and approved scope
Implement a repository custom Dispatcher Agent, not a program. The user confirmed one dispatcher invocation per issue and authorized changing PRI-V instructions so human review happens only at the final PR. Existing tests need no dispatcher-specific folder. No production C# or project/solution changes are needed.

## Files
- Add `.github/agents/dispatcher.agent.md` and `.github/skills/dispatcher/SKILL.md`.
- Standardize the existing six agents as valid `*.agent.md` profiles with exact lowercase names, supported tool aliases and automatic return to Dispatcher.
- Update Prime/Plan/Implement/Validate/Code Review skills, testing placement guidance and repository workflow conventions.
- Add `docs/dispatcher.md`; ignore local `.dispatcher/` run artifacts.
- Preserve existing milestone backlog documents and historical implementation plans.

## Execution
1. Add dispatcher selection/preflight, dry-run, real delegation and single-issue scope.
2. Define per-worker context/result contracts and automatic Prime -> Plan -> Implement -> Testing -> Validate -> Code Review handoffs.
3. Specify deterministic priority/blocker/duplicate checks, isolated issue worktree and persisted reports.
4. Share a default two-repair budget across failed validation/review, rerunning verification after fixes.
5. Require actual successful checks before conventional commit, push and draft PR with final pending human signoff.
6. Validate profile metadata, exact worker references, paths/tool restrictions and absence of obsolete intermediate approval instructions.

## Acceptance coverage
- Issue selection and optional issue/dry-run parameters: Dispatcher skill sections 1–2.
- Branch provisioning and stages: sections 3–4 plus exact named worker profiles.
- Configurable bounded repair: section 5.
- Commit/push/draft PR/independent reviewer: section 6.
- One call and final-only human gate: agent, skill, all worker definitions and repo conventions.
- Setup/runtime limits and smoke scenarios: docs/dispatcher.md.

## Validation and limits
Use static checks for valid frontmatter, existing skill paths, exact delegation names, no stale pseudo-tool references/intermediate gates and no generated CLI artifacts. Inspect diff and whitespace with Git. Instruction-only changes do not justify synthetic unit tests or a separate dispatcher test project. The baseline solution build passed with zero warnings/errors before changes; no application runtime behavior was changed. Live paid dispatch and GitHub publication require an authenticated host with delegation and are not part of this implementation verification.

## Verification performed
- Validated seven named agent profiles, descriptions, parseable tool lists and corresponding skill paths.
- Verified the dispatcher references six existing worker names and Code Review has read/search tools only.
- Checked active agents/skills/conventions for obsolete intermediate approval instructions; removed the conflicting repository gate.
- Verified local run artifacts are ignored and the accidentally started CLI project/files have been removed.
- Checked Markdown fences and Git whitespace. No live paid dispatch, issue mutation, push or PR creation was performed.
