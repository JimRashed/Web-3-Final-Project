# Issue #66 (D05) plan: issue traceability, acceptance evidence and board completeness

Parent F07 #15, epic #7. Taken over from Jim. D03 #64 is still open; this records the current state and names what remains.

## Files
| Path | Change |
|---|---|
| scripts/traceability/build-traceability.py | Reads issues and PRs from GitHub (gh) and writes the per-issue map plus a JSON export, with automatic checks (assignee, parent, closed without merged PR, open with merged PR) |
| docs/milestone3/requirements-traceability.md | Deliverables → issues/PRs/evidence, one full trace per member, audit findings, open items, generated per-issue map |
| docs/milestone3/traceability.json | Machine-readable export (board evidence that doesn't depend on the private project's visibility) |

## Validation
- Export counts match the plan: 1 epic, 8 features, 54 tasks (63).
- No issue without an assignee; no task without a parent.
- Follow one full trace per member by hand (in the doc).
- Rerun the generator after the remaining PRs merge.
