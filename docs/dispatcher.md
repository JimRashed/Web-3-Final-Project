# Dispatcher Agent

Issue #6 is implemented as a custom agent at `.github/agents/dispatcher.agent.md`, backed by `.github/skills/dispatcher/SKILL.md`. There is no dispatcher executable or wrapper program.

## Use

In a host that loads repository custom agents, select **dispatcher** and send one request:

```text
Dispatch the next eligible open issue assigned to me.
```

Or:

```text
Dispatch --issue 42
Dispatch --assignee JimRashed --max-retries 2
Dispatch --issue 42 --dry-run
```

These are agent prompts, not terminal commands. The issue's example `agent dispatch` is an interaction concept; no program with that command was installed.

One request runs Prime -> Plan -> Implement -> Testing -> Validate -> Code Review, including bounded repairs, then opens a draft PR for the only human review gate. No manual stage handoffs or approval of the plan are needed. Worker names match their frontmatter: prime, plan, implement, testing, validate, code-review.

## Prerequisites

- A host with custom-agent discovery and genuine subagent delegation enabled.
- Authenticated GitHub MCP read/write tools or `gh`, with access to assigned issues, Project status, dependency links, remote branches and PR creation.
- Git, .NET 10 and the existing test environment (SQL Server LocalDB for the current integration suite).
- Permission to create/push issue branches and create draft PRs. Provider billing/runtime tool approvals still apply.

Load `.github/agents/*.agent.md` in the IDE's custom-agent picker. The agent uses documented tool aliases including `agent` for delegation. If a host cannot load these profiles directly, it must explicitly load the files as worker instructions with equivalent real delegation; the files do not register tools themselves.

## Defaults

- Assignee: authenticated GitHub user; never inferred from local Git email.
- Selection: open, assigned, sufficiently specified and unblocked; exclude in-progress work or existing issue branches/open PRs. Normalize p0/p1/p2 labels, then oldest first.
- Base: explicit user/repo choice, otherwise the existing verified milestone integration branch for this milestone, otherwise repository default branch.
- Branch: `feat/<issue-id>-<slug>` in an isolated worktree. Caller changes are preserved.
- Repairs: two additional cycles after the initial attempt, shared across validation and review failures.
- Reports: ignored `.dispatcher/runs/<issue>-<run-id>/`; reviewable plan: `specs/issue-<number>-plan.md`.
- Final result: conventional commit, pushed branch, draft PR with issue link/actual validation/review evidence; human signoff remains pending. No automatic merge.

## Read-only dry run

Dry-run checks identity, issue eligibility, dependencies and duplicate work, then reports the proposed branch/base/pipeline. It does not write files, change Git state, launch paid worker agents or publish anything.

## Stop and recovery

Missing requirements, missing tools/auth, exhausted credits, permission failures or failed checks after the retry limit produce a truthful BLOCKED/FAIL report. Worktree/reports remain available. A subsequent continuation must inspect saved state and existing branches/PRs before reusing work; no duplicate publication or destructive reset. There is no programmatic resume guarantee.

The current author cannot review their own PR: assign the issue owner and request a verified independent team member. Automated Code Review does not count as human signoff.

## Acceptance walkthroughs

These are future runtime smoke checks, not claims that live dispatches have been run:
1. Mix eligible p0/p1/old issues, open dependencies, in-progress Project items and linked PRs. Dry-run picks only the highest-priority eligible issue; #6 must not match #60. Unlabeled issues follow labeled issues.
2. Explicitly select an assigned issue; it bypasses ranking but rejects blockers/duplicate work. Empty selection makes no changes.
3. Run a disposable test issue and verify sequential real worker invocations and uninterrupted automatic handoff. Check the scoped plan, tests, actual validation and read-only findings.
4. Cause validation/review failures. Verify at most two extra Implement/Testing/Validate/Review cycles and no publication after exhausted retries. A changed implementation always gets fresh validation/review.
5. Check unrelated caller changes stay untouched and dry-run creates no reports/worktree.
6. Inspect final draft PR: correct base/head, Conventional Commit, `Closes #n`, plan checklist, actual check results, review report, pending human signoff and independent reviewer.
7. Remove GitHub/delegation access or interrupt a run. Expect a concrete blocker/checkpoint rather than fabricated successful stages or a duplicate PR.

## Configuration references

- [GitHub custom-agent configuration](https://docs.github.com/en/copilot/reference/custom-agents-configuration)
- [VS Code subagent support and exact-name delegation](https://code.visualstudio.com/docs/agents/run/subagents)

Tool availability and approval behavior depend on the host. Agent instructions coordinate work; they cannot bypass host permissions or provider usage limits.
