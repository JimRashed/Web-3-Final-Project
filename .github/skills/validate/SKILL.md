---
name: validate
description: Executes test suites, analyzes test results, checks for regressions, and compiles a comprehensive validation summary report.
---

# Validation Protocol & Execution Standard

Use this skill to run and verify the complete test suite against local changes before opening a pull request.

---

## 1. Automated Execution Commands

Execute the test suites via the CLI using these standard commands:

    # Standard test execution with detailed logger
    dotnet test --logger "console;verbosity=detailed"

    # Run tests and generate code coverage report
    dotnet test --collect:"XPlat Code Coverage"

---

## 2. Verification Checklist

The agent must independently verify all of the following criteria:

1. **Full Pass Rate:** 100% of existing regression tests and newly authored tests must pass with zero failures.
2. **Coverage Compliance:**
   - **Happy Path:** Valid input scenarios resulting in expected `200 OK` or `201 Created` statuses.
   - **Validation / Guard Clause:** Malformed input, null checks, or boundary values producing `400 Bad Request`.
   - **Error / Resource States:** Missing entities returning `404 Not Found`, or conflicts returning `409 Conflict`.
3. **No Flakiness or Ordering Dependencies:** Tests must pass when executed in parallel or independently.
4. **Clean Diff Check:**
   - Zero compilation warnings.
   - No leftover debugging statements, temporary logging, or commented-out code.
   - No hardcoded magic values where named constants belong.

---

## 3. Failure Remediation Protocol

If any test run fails or throws an unhandled exception:

1. **Root Cause Analysis:**
   - Determine whether the failure originates from production logic, incorrect test assertions, or environment misconfiguration (such as in-memory context state leakage).
2. **Minimal Correction:**
   - Report the targeted correction to Dispatcher for Implement; Validate must not edit production code or tests.
   - **Strict Rule:** Never disable, skip (`[Fact(Skip = "...")]`), or delete an existing test to force a pass.
3. **Re-Verification:**
   - After Implement/Testing repairs, Dispatcher invokes Validate again to re-run the required checks and verify regressions. Share the Dispatcher's bounded repair budget; do not loop indefinitely.

---

## 4. Deliverable: Validation Summary Report

Upon test completion, output a structured Markdown report in the following format:

### Validation Summary

| Metric | Result | Target | Status |
| :--- | :--- | :--- | :--- |
| **Total Tests Executed** | `[Count]` | `All discovered` | Pass / Fail |
| **Passed Tests** | `[Count]` | `100%` | Pass / Fail |
| **Failed / Skipped** | `0` | `0` | Pass / Fail |
| **Build Warnings** | `0` | `0` | Pass / Fail |

#### Scenarios Verified
- [ ] **Unit Tests:** Verified domain/service logic and isolated mocks.
- [ ] **Integration Tests:** Verified HTTP endpoints and status codes via `WebApplicationFactory`.
- [ ] **Edge Cases:** Verified null/boundary inputs and validation filters.

#### Remediation Notes
*(List any test failures encountered during the run and what corrective actions were applied, or state "None — all tests passed on first execution.")*

#### PR Readiness Verdict
**Ready for Pull Request** / **Changes Required**

## Dispatcher result
Return PASS/FAIL/BLOCKED with exact commands, exit codes, warnings, actual discovered/passed/failed/skipped counts and failed acceptance criteria. Missing environment/access is BLOCKED; never claim success from historical results or treat an agent promise as executed validation. Match checks to the issue (including static/manual verification for instruction-only changes). Return evidence directly to Dispatcher for repair or read-only Code Review without a human gate. Human signoff remains pending until the final PR review.
