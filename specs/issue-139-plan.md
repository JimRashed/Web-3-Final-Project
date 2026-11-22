# SEC-A3 threat model implementation plan

## Summary and architecture

Issue [#139](https://github.com/JimRashed/Web-3-Final-Project/issues/139) requests a threat model for QuizArena's security milestone. The PR targets the Milestone 4 integration branch (`feature/milestone-4-security-and-cross-device-profiles`). The worktree starts at `deef7c0`, which descends from the issue's `c8dcc78` historical baseline and includes the merged #137, #140 and #141 work. The plan is agent-reviewed. Independent review is pending on the draft PR.

The document covers two independent browser sessions, Keycloak, protected routes on all six API hosts, Gameplay service-to-service traffic, and Identity profile and private picture storage. It describes observed controls and labels proposed picture behavior and outstanding verification.

## File inventory

| Path | Change |
| --- | --- |
| `specs/issue-139-plan.md` | Record this plan and the acceptance mapping. |
| `docs/milestone3/threat-model.md` | Create the threat model, mitigation and issue table, and residual-risk register. |

Read the security decisions, authorization matrix, OpenAPI contracts, Keycloak realm, browser session code, server auth policies, Gameplay machine client and outbox, and profile persistence. Keep production, tests, contracts, and existing decisions unchanged.

## Ordered steps

1. Establish the branch and design baseline. Cite the merged decisions and authorization matrix and record that #137 is closed and the decision document still has pending-review wording. Verify each trust boundary against the code or contract for that boundary.
2. Document the flows. Trace independent device logins through Keycloak, browser memory token handling, request-level bearer checks, refresh and sign-out, affected device scope, and the persistence boundary for shared profile data. Trace Gameplay machine calls to Lobby, QuestionBank, Statistics and Notification with the destination-specific actor, scope and audience. Confirm the Identity picture design uses owner-authorized bearer delivery and revocable client object URLs. Cross-check each route family with the route matrix.
3. Write a table with a threat, affected boundary, mitigation, delivery state, and at least one linked implementation or verification issue on each row. Include token validation and exposure, actor and scope checks, ownership and identity, configuration and logs, profile privacy and consistency, upload parsing and resource limits, private storage and caching, sign-out and session lifecycle. Cross-check links to #138 and #140–#147 and the evidence reconciliation in #148. Where a risk has no owner, identify the decisions register entry and record the gap.
4. Record residual risks and decisions. Include remaining access-token lifetime, shared browser-token audience reach, server logout failure and device independence, picture dimensions and metadata, slow uploads, storage availability and DR-8. Explain that SEC-08 has no server-issued temporary links, so issuance and expiry rules do not apply. Validate every claim about current and proposed controls against source and record unresolved status explicitly before handoff.
5. Run a read-only acceptance check and record actual command results for Dispatcher. Review all new Markdown and linked issues against the accepted scope. Hand the document and evidence to independent review for route, storage and session accuracy. #148 will reconcile with the integrated milestone.

## Acceptance coverage

| Criterion | Plan coverage |
| --- | --- |
| Browser, Keycloak, API and storage boundaries, including Gameplay internal calls and two independent sessions | Steps 1–2 |
| Token validation, privilege, ownership, profile data, parsing/storage, sign-out and session lifecycle | Steps 2–3 |
| One or more implementation or verification issue links for every threat | Step 3 |
| Residual risks and decisions, including temporary-link controls if applicable | Step 4 |
| Route and storage contract review and D3 reconciliation | Steps 2 and 5 |

## Testing strategy and validation

Documentation checks include a manual crosswalk to the authorization matrix and the picture contract, verifying issue numbers and trace links, checking local relative links, and reading the diff for inaccurate state, accidental secret values or scope drift. Confirm there are no changes to production or test inputs. There are no new runtime behaviors to cover with additional automated scenarios.

Run from the worktree root and record exact exits, findings, and available test totals.

```powershell
git status --short
git diff --check
node contracts/validate-contracts.mjs
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal
```

Review Markdown files directly while untracked and check the staged diff for whitespace before publication. The solution gates require the repository's .NET 10 and test dependencies. The contract validator requires Node and its declared dependency. Frontend source is unchanged.

## Risks and exclusions

Use the merged design documents while preserving their stated review status. Picture implementation and live two-device acceptance are tracked separately. A valid bearer token can outlive browser cleanup, and a failed redirect may leave the provider session open. Follow SEC-08 for picture access. Record any outstanding control as pending or residual when the current branch or verification evidence does not establish delivery. Keep all recommendations defensive and scoped to the document. Independent human approval and milestone acceptance remain pending.
