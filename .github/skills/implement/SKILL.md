---
name: implement
description: Executes an approved implementation plan incrementally in a fresh session, adhering to repository testing conventions.
---

# Implementation Execution Workflow

Use this skill in a fresh agent session with an approved plan artifact.

## 1. Context Loading
1. Load `specs/implementation-plan.md` as the single source of truth.
2. Verify the project build status using `dotnet build` before making changes.

## 2. Incremental Execution
Follow the task sequence step-by-step:
1. Implement the domain/service logic changes first.
2. Add or update unit tests following the AAA pattern in `.github/skills/testing/SKILL.md`.
3. Add integration tests using `WebApplicationFactory`.
4. Keep changes focused strictly on the files identified in the plan. Avoid speculative refactoring.

## 3. Intermediate Verification
After each meaningful step, confirm the code compiles and tests pass before continuing to the next step.