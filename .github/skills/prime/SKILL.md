---
name: prime
description: Researches a GitHub issue, explores codebase dependencies, and conducts an interview to eliminate assumptions before planning.
---

# Prime & Research Workflow

Use this skill when starting work on a new GitHub issue.

## 1. Context Ingestion
1. Retrieve the issue details using GitHub MCP tools (`get_issue` or `search_issues`).
2. Identify the core user story, functional requirements, and constraints.
3. Check for any related issues or past PRs that touch the same components.

## 2. Codebase Investigation
1. Search and inspect the production and test files relevant to the issue.
2. Note key interfaces, dependencies, data structures, and existing test patterns.
3. Do not modify or write any code during this phase.

## 3. Developer Interview (Assumption Reduction)
Before moving to planning, restate the problem and ask 2–4 targeted clarification questions:
- Boundary conditions and edge cases.
- Preferred implementation patterns or library choices.
- Scope limits (what explicitly should *not* be touched).

Wait for user confirmation and answers before proceeding to the plan phase.