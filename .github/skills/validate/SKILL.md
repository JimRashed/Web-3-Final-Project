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
   - Apply the targeted, minimal fix required to resolve the failure.
   - **Strict Rule:** Never disable, skip (`[Fact(Skip = "...")]`), or delete an existing test to force a pass.
3. **Re-Verification:**
   - Re-run the full test command (`dotnet test`) to verify the fix does not introduce regressions elsewhere.

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