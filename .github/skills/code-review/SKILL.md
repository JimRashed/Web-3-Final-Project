---
name: code-review
description: Review pull request diffs and staged code changes against security, testing coverage, architectural consistency, and error handling. Produces a structured audit log.
---

# Code Review Protocol

When requested to review a diff, branch, or pull request, conduct a structured evaluation and produce the required findings deliverable.

## 1. Review Dimensions

Evaluate changes across five distinct axes:
1. **Correctness & Contract Adherence:** Does the code satisfy the issue requirements? Do response types, status codes, and models match the spec?
2. **Test Completeness:**
   * Are both unit tests and integration tests included?
   * Are edge cases (nulls, empty collections, invalid inputs, boundary values) covered?
   * Are assertions specific, or do tests pass trivially?
3. **Architecture & Resource Safety:**
   * Proper disposal of unmanaged resources (`using` blocks / `IAsyncDisposable`).
   * No blocking async calls (`.Result`, `.Wait()`, or `GetAwaiter().GetResult()`).
   * Correct dependency injection lifetimes (`Scoped`, `Transient`, `Singleton`).
4. **Security & Validation:**
   * Input sanitization and model validation applied before domain logic runs.
   * No sensitive credentials or hardcoded keys exposed.
5. **Code Style & Maintainability:**
   * Clean separation of concerns.
   * Consistent C# naming conventions (PascalCase for methods/properties, camelCase for parameters).

---

## 2. Review Findings Output Format

Always conclude the review with a Markdown table using this schema:

| ID | Category | Severity | File / Location | Description | Action Status |
|:---|:---|:---|:---|:---|:---|
| #1 | Testing | High | `tests/.../OrderTests.cs:42` | Missing negative test case for empty payload. | **Fixed** / **Deferred** |
| #2 | Performance | Medium | `src/.../OrderService.cs:18` | Synchronous `.Result` call on `Task` risks thread pool starvation. | **Fixed** / **Deferred** |
| #3 | Style | Low | `src/.../IOrderService.cs:7` | Parameter name doesn't match implementation. | **Fixed** / **Deferred** |

### Action Status Guidelines
* **Fixed:** Resolved directly in the PR before merge. Include a one-line note on the fix.
* **Deferred:** Out-of-scope for the milestone or low-impact. Provide a concrete justification why it is acceptable to defer (e.g., tracked in a future backlog item).