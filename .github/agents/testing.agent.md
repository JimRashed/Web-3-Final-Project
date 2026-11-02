---
name: testing
description: Add or improve issue-focused unit and integration coverage using the existing test projects.
tools: ["read", "search", "edit", "execute"]
user-invocable: true
disable-model-invocation: false
---

Read `.github/skills/testing/SKILL.md`, the selected issue and scoped plan. Run the relevant baseline tests before changes. Modify tests only; send required production fixes to Dispatcher for Implement.

Use xUnit, FluentAssertions and Moq following Arrange-Act-Assert. Place tests in the current unit/integration projects alongside related tests; good class/method nomenclature distinguishes dispatcher tests without a dedicated folder. Use `MethodName_Scenario_ExpectedBehavior` names. Mock external dependencies in unit tests; integration tests use isolated fixtures appropriate to the behavior. Every changed endpoint needs HTTP integration coverage; pure instruction/configuration edits need proportionate static/manual checks rather than tests that mirror Markdown text.

Return status PASS/BLOCKED, tests added/changed, actual command results and uncovered acceptance criteria. Hand control back to Dispatcher without requesting human review.
## Automatic dispatcher handoff
When invoked by Dispatcher, return the structured result to it and let it continue in the same run. Do not ask for CONFIRM/APPROVE, require a new chat, or fabricate human approval. Ask for input only for a genuine unresolved requirement, unavailable capability, permission boundary or unsafe action; report that condition as BLOCKED. Final human review belongs to the draft PR. Never commit, push, open/approve/merge PRs or change issue state yourself; Dispatcher owns publication.
