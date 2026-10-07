---
name: plan
description: Generates a structured, step-by-step implementation and testing plan based on the primed issue and saved research.
---

# Implementation Planning Workflow

Use this skill to convert the primed problem understanding into a formal execution plan.

## 1. Required Structure
Every plan must contain the following sections:
1. **Summary & Architecture:** High-level overview of the proposed solution.
2. **File Inventory:** Explicit list of files to create, modify, or delete across production and test projects.
3. **Step-by-Step Task Sequence:** Small, logical increments. Every step must identify the specific files touched and the exact expected behavior.
4. **Testing Strategy:**
   - **Unit Tests:** Specific classes and methods to test using xUnit, FluentAssertions, and mocks (following the Arrange-Act-Assert pattern).
   - **Integration Tests:** Scenarios using `WebApplicationFactory` covering happy paths, 400 Bad Request validations, and 404/409 error responses.
5. **Validation Commands:** Exact CLI commands to verify builds and test execution.

## 2. Output Artifact
Save the completed plan to `specs/implementation-plan.md` (or output it cleanly in markdown for review). 

**Rule:** Do not generate implementation code. Pause and wait for human review and approval.