---
name: plan
description: Produce an issue-scoped file-by-file plan and hand it automatically to implementation.
---

# Implementation Planning
Review the Prime brief against the issue. Verify that every acceptance criterion is addressed and that the approach respects repository boundaries. Resolve routine choices from evidence; report genuine scope-changing gaps as BLOCKED.

Save the plan to `specs/issue-<number>-plan.md`. Preserve historical `specs/implementation-plan.md`; use a supplied scoped plan path when explicitly provided.

Required sections:
1. **Summary and Architecture:** issue link, intended behavior and layer boundaries.
2. **File Inventory:** exact files to create/modify/delete across production and existing test projects.
3. **Ordered Steps:** goal, files, method/class behavior and verifiable completion check for each step.
4. **Acceptance Coverage:** map every criterion to one or more steps; fix omissions before handoff.
5. **Testing Strategy:** meaningful xUnit/FluentAssertions/Moq unit scenarios and HTTP/persistence integration scenarios as applicable. Use existing test locations and descriptive names; no dispatcher-specific test folder. Instruction-only changes use static/manual checks.
6. **Validation Commands:** exact build/format/test/frontend commands and required environment; never invent a historical test count as current evidence.
7. **Risks and Out of Scope:** unresolved blockers, constraints and excluded behavior.

Do not write implementation code in this stage. Return PASS with the scoped plan path or BLOCKED with evidence. In dispatcher mode, the Dispatcher reviews plan completeness and proceeds directly to Implement; no intermediate human approval or separate user invocation. Record it as agent-reviewed, never human-approved.
