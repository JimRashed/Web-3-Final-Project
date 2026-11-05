# Milestone 3 submission bundle (D07/D08)

Issues [#68](https://github.com/JimRashed/Web-3-Final-Project/issues/68) (D07) and [#69](https://github.com/JimRashed/Web-3-Final-Project/issues/69) (D08), parent F07 #15, epic #7. Everything the course submission needs, using GitHub equivalents (no Azure DevOps). **The course-system (Lea) submission is done by a human team member. It is not claimed here.**

## Links

| Deliverable | Link |
|---|---|
| Repository | https://github.com/JimRashed/Web-3-Final-Project |
| Retained milestone branch | [`feature/milestone-3-layered-service-development`](https://github.com/JimRashed/Web-3-Final-Project/tree/feature/milestone-3-layered-service-development) (automatic branch deletion is off) |
| Milestone PR into `main` | [#114](https://github.com/JimRashed/Web-3-Final-Project/pull/114): merged 2026-11-05 00:20 UTC, merge commit `1b6c8f2683`, CI on `main` passed |
| Follow-up PR into `main` | [#120](https://github.com/JimRashed/Web-3-Final-Project/pull/120): merged 2026-11-05 01:13 UTC, merge commit `9441036f52`, CI on `main` passed |
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

Both PRs into `main` were merged by SuSu-Lisu with a **comment review** from CarterBeasse, not an approval. The review on #114 asked to wait for D02, D03, D05 and D06. The review on #120 asked to wait for #118. Every task PR into the milestone branch had at least one teammate review; who approved each one is in the generated map in [requirements traceability](requirements-traceability.md#per-issue-map).

## State at the time of writing (2026-11-05)

`main` contains the six services, the React client, CI, the report, the runbook and every merged task. These task PRs into the milestone branch are **open and not yet on `main`**:

| PR | Why it matters |
|---|---|
| [#118](https://github.com/JimRashed/Web-3-Final-Project/pull/118) Gameplay machine-token fix | **Without it, every match start on `main` returns 503.** The options bound the wrong config keys, and the OAuth token response was read as camelCase. |
| [#121](https://github.com/JimRashed/Web-3-Final-Project/pull/121) Guest opens the match from events, plus the D03 journey scripts | Without it, the guest has no in-app way to reach the match. The scripts prove D03 (11/11 browser, 10/10 API, 8/8 retry, all with #118). |
| [#115](https://github.com/JimRashed/Web-3-Final-Project/pull/115) D02 legacy cutover, retiring the aggregate host | Data-migration deliverable (34/34 rehearsal checks) |
| [#119](https://github.com/JimRashed/Web-3-Final-Project/pull/119) D05 traceability | Audit deliverable |

Once those merge into the milestone branch, a **final follow-up PR into `main`** brings `main` up to date. That merge needs D06 (#67, review and signoff) and the team's explicit go-ahead.

## Closure checklist

- [x] Repository, retained branch and milestone PR are verifiable (links above)
- [x] Report, runbook, traceability, data-cutover and validation docs are in `docs/milestone3/`
- [x] Board evidence is exported (`traceability.json`)
- [ ] #118, #121, #115 and #119 merged into the milestone branch
- [ ] D03 #64 accepted by its owner (evidence is posted on the issue)
- [ ] D06 #67 review and signoff
- [ ] Final follow-up PR into `main` merged, then CI checked on `main`
- [ ] Epic #7 and feature checklists updated truthfully; open items stay open
- [ ] A human team member submits the merged milestone PR URL in Lea
