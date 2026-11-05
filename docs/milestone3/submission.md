# Milestone 3 submission bundle (D07/D08)

Issues [#68](https://github.com/JimRashed/Web-3-Final-Project/issues/68) (D07) and [#69](https://github.com/JimRashed/Web-3-Final-Project/issues/69) (D08), parent F07 #15, epic #7. Everything the course submission needs, using GitHub equivalents (no Azure DevOps). **The course-system (Lea) submission is done by a human team member. It is not claimed here.**

## Links

| Deliverable | Link |
|---|---|
| Repository | https://github.com/JimRashed/Web-3-Final-Project |
| Retained milestone branch | [`feature/milestone-3-layered-service-development`](https://github.com/JimRashed/Web-3-Final-Project/tree/feature/milestone-3-layered-service-development) (automatic branch deletion is off) |
| Milestone PR into `main` | [#114](https://github.com/JimRashed/Web-3-Final-Project/pull/114): merged 2026-11-05 00:20 UTC, merge commit `1b6c8f2683`, CI on `main` passed |
| Follow-up PRs into `main` | [#120](https://github.com/JimRashed/Web-3-Final-Project/pull/120) (merge commit `9441036f52`) and [#123](https://github.com/JimRashed/Web-3-Final-Project/pull/123) (merge commit `0b5580ac48`, carries #115, #118, #119, #121 and #122). CI on `main` passed. |
| Milestone issues (63: 1 epic, 8 features, 54 tasks) | [milestone filter](https://github.com/JimRashed/Web-3-Final-Project/issues?q=milestone%3A%22Milestone+3+%E2%80%94+Layered+Service+Development%22) |
| Board | [project board](https://github.com/users/CarterBeasse/projects/1) (private to its owner); shareable export: [`traceability.json`](traceability.json) |
| Traceability and audit | [requirements traceability](requirements-traceability.md) |
| Architecture, security and reflection report | [architecture-and-reflection.md](architecture-and-reflection.md) |
| Validation and security evidence | [validation results](validation-results.md), [authentication](authentication.md), [authorization matrix](authorization-matrix.md) |
| Local run | [runbook](runbook.md), `scripts/start-milestone3.ps1` / `stop-milestone3.ps1` |
| End-to-end journeys | `tests/e2e` (browser journey, API journey, delivery retry), recorded in the runbook |
| Legacy data cutover | [data cutover](data-cutover.md), `scripts/migrate-legacy-data/` |
| CI | [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml), [Actions runs](https://github.com/JimRashed/Web-3-Final-Project/actions) |

## Review log for the `main` merges

All three PRs into `main` were merged by SuSu-Lisu. #114 and #120 had a **comment review** from CarterBeasse, not an approval (#114's asked to wait for D02, D03, D05 and D06; #120's asked to wait for #118). #123 had no review. Its contents were each approved as task PRs. Every task PR into the milestone branch had at least one teammate review; who approved each one is in the generated map in [requirements traceability](requirements-traceability.md#per-issue-map).

## State at the time of writing (2026-11-05)

`main` at `0b5580a` contains the whole milestone branch. On that commit the local build has 0 warnings, 1,438 backend tests and 305 client tests pass, and the live end-to-end journeys pass **29/29** (browser 11/11, API 10/10, delivery retry 8/8). Still open: the Notification `GET /events` paging follow-up, D01's clean-checkout run by a teammate, and D06 signoff.

## Closure checklist

- [x] Repository, retained branch and milestone PR are verifiable (links above)
- [x] Report, runbook, traceability, data-cutover and validation docs are in `docs/milestone3/`
- [x] Board evidence is exported (`traceability.json`)
- [x] #118, #121, #115 and #119 merged into the milestone branch and onto `main` (#123)
- [ ] D03 #64 accepted by its owner (evidence on the issue; journeys pass on `main`)
- [ ] D06 #67 review and signoff
- [x] Final follow-up PR into `main` merged (#123); CI passed on `main`
- [ ] Epic #7 and feature checklists updated truthfully; open items stay open
- [ ] A human team member submits the merged milestone PR URL in Lea
