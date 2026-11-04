# Milestone 3 — Architecture and reflection

This document explains the architecture we built, how the work was divided, how the repository
agents changed the way we worked, and what the human validation gates actually caught.

## 1. Architecture and the frontend choice

The solution is six independently runnable services, each split into `Domain`, `Data` and `Api`,
plus one React client: Identity and Statistics (Carter), QuestionBank and Lobby (Jim), Gameplay and
Notification (Suheng).

**Dependency direction is enforced, not just documented.** `Domain` holds entities, invariants and
repository *ports* and has no package, framework or project reference at all. `Data` references only
its own `Domain` and owns the `DbContext`, the Fluent API configuration, the migrations and the
repository implementations. `Api` references its own `Data` and `Domain` and owns HTTP contracts,
application orchestration and authorization. An architecture test reads the project files and fails
the build if a reference points the wrong way, if `Domain` gains a package, or if a service test
project references anything other than its own layer and the shared test fixture.

**The database boundary is per service.** Each service owns one database, one `DbContext` and its
own migration lineage. Identifiers are opaque strings or GUIDs, never foreign keys across services,
and cross-service data arrives through HTTP or not at all. The shared test fixture is allowed to be
shared precisely because it contains no production contract.

**React was chosen for the client** because the OAuth2/OIDC authorization-code flow with PKCE is a
browser concern: the client obtains tokens from Keycloak, attaches them as bearer tokens and never
talks to a database. It validates every response before it reaches the UI, so contract drift shows
up as a visible error state rather than as undefined behaviour.

## 2. How the work was divided

We split by **service ownership**, not by layer. Each member owns two services end to end — domain,
persistence, API and the matching client screens. That avoids the failure mode where three people
edit the same layer in three services and nobody owns a complete slice.

Contracts came first: one member produced the OpenAPI documents and the authorization matrix, and
the team approved them before implementation. Once the contracts were committed, parallel work
became safe — the frontend could be written against DTOs that already existed, and each service
could be implemented without waiting for its peers.

Service dependencies are explicit and ordered: Gameplay calls Lobby (verify the lobby is locked and
the caller is the host) and QuestionBank (take five immutable question snapshots), then fans the
outcome out to Statistics and Notification. Nothing calls back, and no service shares a context.

## 3. Where the agents helped — and where we corrected them

We used the repository agents (prime, plan, implement, testing, validate, code-review, dispatcher)
on the Research → Plan → Implement → Validate loop, each constrained by its own instructions.

**What they accelerated**

- Scaffolding an eighteen-project solution with consistent namespaces, ports and host entry points
  took minutes rather than an afternoon.
- Repetitive per-service work — a `DbContext`, a configuration, a migration, a repository with the
  same duplicate-key recovery — came out consistent across six services.
- Tests arrived with the implementation rather than after it, because testing is part of the loop.

**Where they needed human correction**

- **Typed payloads.** An agent implemented the notification event with a free-form JSON payload.
  Review caught that the contract requires two typed events with a `matchId`, an optional winner and
  a `draw` that must agree with the winner, and that preferences default to *disabled*. The fix
  changed the domain model, not just the mapping.
- **Test-only bugs.** An agent wrote an upsert test that ran both writes on one `DbContext`; EF's
  change tracker made the second call mutate the object the first assertion was about.
- **A real lost-update bug.** The Gameplay persistence agent implemented load-modify-save without a
  concurrency token; two players answering the same round would silently overwrite one answer. The
  fix added a `rowversion` token and a `MatchConcurrencyException` so a stale write is refused.

The pattern is consistent: agents are strong at structure, repetition and consistency, and weak at
the parts of a specification that are easy to skim — a default value, a conditional invariant, a
concurrency guarantee. Those are exactly what a human reviewer must read line by line.

## 4. How effective the human gates were

We ran three gates per work item: plan review before implementation, diff and test review after,
and an independent peer review before merge.

**What the gates caught that automation did not**

- The typed-payload and default-value mismatches were *specification* errors. Every test the agent
  wrote passed; the tests asserted what the agent had assumed. A second human reading the acceptance
  criteria was the only defence.
- The lost-update bug was a *concurrency* error: single-threaded tests pass, and the reviewer had to
  reason about two requests at once.
- Review kept the shared fixture honest: allowing one test-support reference per test project needed
  an explicit architecture-test change, exactly the kind of small exception that can become a back door.

**What the gates did not do well**

- Early plans were rushed. A plan that says "add the entity and the repository" satisfies the form
  but does not force the author to re-read the acceptance criteria. The plans that worked listed
  rules as testable statements.
- The build environment was a bottleneck: the development VM blocks the test host, so `dotnet test`
  could not run locally. Once CI existed, "the tests pass" stopped being a claim and became a link —
  that single change improved review quality more than any prompt.

## 5. What we would change

1. Treat the acceptance criteria as the input of the plan, not a checklist at the end: every bullet
   becomes one named test before implementation starts.
2. Run CI from the first service rather than after the second; the feedback loop is the cheapest
   correctness tool available.
3. Give every agent an explicit "stop and ask" rule for anything the specification does not state,
   because that is where it is most likely to invent a plausible wrong answer.
