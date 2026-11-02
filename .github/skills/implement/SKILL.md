---
name: implement
description: Execute an issue-scoped researched plan or dispatcher repair request without an intermediate approval gate.
---

# Implementation
1. Load repository conventions, the issue and its exact `specs/issue-<number>-plan.md` supplied by Dispatcher. Do not default to an unrelated historical plan. Read relevant prior reports and repair feedback.
2. Run a baseline build and relevant baseline tests; record existing failures separately from new regressions. Fix in-scope environment problems, or return a concrete BLOCKED result.
3. Implement the plan incrementally: pure rules, persistence, application/API and client changes as applicable. Keep files/scope aligned with the plan and explain necessary adjustments.
4. Add/update meaningful tests using `.github/skills/testing/SKILL.md`; use existing test projects/locations with descriptive names rather than a dispatcher-specific folder.
5. Run focused checks after meaningful changes. Never skip/delete tests, weaken authorization, fabricate results or expand scope to force green.
6. Return PASS/BLOCKED with changed paths, behavior, actual check evidence, risks and plan adjustments. Hand off automatically to Dispatcher for Testing/Validate.
7. Workers do not commit, push, create PRs, approve or merge. Dispatcher owns final publication after validation/review. Human review happens at the draft PR, not between stages.
