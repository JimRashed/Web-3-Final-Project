# Milestone 3 — Architecture and reflection

This document explains the architecture we built for Milestone 3, how the work was divided, how
the repository agents changed the way we worked, and what the human validation gates actually
caught. It is written from the team's experience, not from the specification.

## 1. Architecture and the frontend choice

The solution is six independently runnable services, each split into `Domain`, `Data` and `Api`,
plus one React client:

| Service | Owns | Owner |
|---|---|---|
| Identity | player profiles | Carter |
| Statistics | match results, totals, leaderboard | Carter |
| QuestionBank | questions, categories, snapshots | Jim |
| Lobby | lobbies, membership, lock state | Jim |
| Gameplay | matches, rounds, answers, outcome | Suheng |
| Notification | player events and preferences | Suheng |

**Dependency direction is enforced, not just documented.** `Domain` holds entities, invariants and
repository *ports*; it has no package, framework or project references at all. `Data` references
only its own `Domain` and owns the `DbContext`, the Fluent API configuration, the migrations and
the repository implementations. `Api` references its own `Data` and `Domain` and owns HTTP
contracts, application orchestration and authorization. A Roslyn-free architecture test reads the
project files and fails the build if a reference points the wrong way, if `Domain` gains a package,
or if a service test project references anything other than its own layer and the shared test
fixture.

**The database boundary is per service.** Each service owns a database, a `DbContext` and its own
migration lineage. Identifiers are opaque strings or GUIDs, never foreign keys across services, and
cross-service data arrives through HTTP or not at all. This is why the shared test fixture project
(`tests/QuizArena.TestSupport`) contains no production contracts: it is allowed to be shared
precisely because it is not a domain assembly.

**React was chosen for the client** because the course requires an independent client layer and
because the OAuth2/OIDC authorization-code flow with PKCE is a browser concern: the client obtains
tokens from Keycloak, attaches them as bearer tokens and never talks to a database. The client
validates every response before it reaches the UI, so a contract drift shows up as a visible error
state rather than as undefined behaviour.

## 2. How the work was divided

We split by **service ownership**, not by layer. Each member owns two services end to end —
domain, persistence, API and the matching client screens — and owns the work items that go with
them. That avoids the classic failure mode where three people edit the same layer in three
services and nobody owns a complete slice.

The contracts came first: one member produced the OpenAPI documents and the authorization matrix,
and the team approved them before implementation (P02). Once the contracts were committed, the
parallel work became safe: the frontend could be written against DTOs that already existed, and
each service could be implemented without waiting for its peers.

Dependencies between services are explicit and ordered: Gameplay calls Lobby (verify the lobby is
locked and the caller is the host) and QuestionBank (take five immutable question snapshots), then
fans the outcome out to Statistics and Notification. Nothing calls back the other way, and no
service shares a context with another.

## 3. Where the agents helped — and where we had to correct them

We used layer-specific agents (domain, data/persistence, API, frontend) plus the existing
Research → Plan → Implement → Validate skills. Each agent was constrained by its own instruction
file: the domain agent may not add packages, the persistence agent may not reference the API, the
API agent may not put business rules in controllers.

**What the agents genuinely accelerated**

- Scaffolding an eighteen-project solution with consistent namespaces, ports and host entry points
  took minutes rather than an afternoon.
- Repetitive per-service work — a `DbContext`, a configuration, a migration, a repository with the
  same duplicate-key recovery — was produced consistently across six services.
- Tests came with the implementation instead of after it, because the testing skill is part of the
  loop rather than a separate phase.

**Where they needed human correction**

- **Typed payloads.** An agent implemented the notification event with a free-form JSON payload.
  The review caught that the contract requires two typed events with a `matchId`, an optional
  winner and a `draw` that must agree with the winner, and that preferences default to *disabled*,
  not enabled. The correction changed the domain model, not just the mapping.
- **Test-only bugs.** An agent wrote an upsert test that ran both writes on one `DbContext`; EF's
  change tracker made the second call mutate the object the first assertion was about. The
  production code was right; the test was wrong.
- **A real lost-update bug.** The Gameplay persistence agent implemented load-modify-save without a
  concurrency token. Two players answering the same round at the same time would silently overwrite
  one answer. The review caught it, and the fix added a `rowversion` concurrency token and a
  `MatchConcurrencyException` so a stale write is refused instead of applied.

The pattern is consistent: agents are strong at structure, repetition and consistency, and weak at
the parts of a specification that are easy to skim — a default value, a conditional invariant, a
concurrency guarantee. Those are exactly the parts a human reviewer must read line by line.

## 4. How effective the human gates were

We ran three gates per work item: plan review before implementation, diff and test review after,
and an independent peer review before merge.

**What the gates caught that automation did not**

- The typed-payload and default-value mismatches above were *specification* errors. Every test the
  agent wrote passed; the tests only asserted what the agent had assumed. A second human reading
  the acceptance criteria was the only defence.
- The lost-update bug was a *concurrency* error. Single-threaded tests pass; the reviewer had to
  reason about two requests at once.
- Review also kept the shared test fixture honest: allowing one test-support reference per test
  project required an explicit architecture-test change, which is exactly the kind of "small
  exception" that can quietly become a back door.

**What the gates did not do well**

- Early on, the plan gate was rushed: a plan that says "add the entity and the repository" satisfies
  the form but does not force the author to re-read the acceptance criteria. The plans that worked
  were the ones that listed rules as testable statements.
- The build environment was a bottleneck. The development VM blocks the VSTest host, so `dotnet
  test` could not run locally at all. Once the CI workflow existed, "the tests pass" stopped being a
  claim and became a link — that single change improved the quality of review more than any prompt.

## 5. What we would change

1. Treat the acceptance criteria as the input of the plan, not as a checklist at the end: every
   bullet should become one named test before implementation starts.
2. Run CI from the first service rather than after the second one; the feedback loop is the
   cheapest correctness tool available.
3. Give every agent an explicit "stop and ask" rule for anything the specification does not state,
   because that is where it is most likely to invent a plausible wrong answer.
