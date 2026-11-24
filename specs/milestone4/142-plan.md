# #142 (SEC-B3) — extend the security matrix in CI

> Issue: #142 · Parent: #134 (SEC-B) · Tracker: #132 · Owner: @SuSu-Lisu
> Branch: `feat/142-authorization-matrix`, base `feature/milestone-4-security-and-cross-device-profiles`.

## What the matrix was missing

Each service already has a `SecurityMatrixTests` that drives the **real bearer handler** over a probe
controller — that proves the middleware works. What nothing proved is the other half: that **every
route in every contract** is declared with a scope, an actor and an audience, that the declaration
matches the scope table the service actually registers, and that no scope is declared but unused.

Without that, a new controller can ship with `[AllowAnonymous]`, or with a policy name that exists only
as a string, and every existing test stays green.

## What this adds

`tests/QuizArena.ArchitectureTests/AuthorizationMatrixTests.cs` reads the committed contracts and the
`*Policies.cs` tables as data and asserts, for all six services:

| Check | Why it matters |
|---|---|
| Every operation declares `x-policy`, `x-actor`, `x-audience` | an undeclared route is an unaudited route |
| The scope exists in `<Service>Policies` with the same actor kind | a typo in either place becomes a 403 in production, or a policy nobody grants |
| The audience is `quizarena-<service>` | a token for another service must never pass here |
| Anonymous is declared twice (`x-policy: public` **and** `security: []`) | an accidentally public route is visible, not silent |
| Every declared policy protects at least one operation | dead policies hide drift after a route is renamed |
| Every scope appears in `docs/milestone3/authorization-matrix.md` | the human-readable matrix cannot fall behind the code |

This is the "equivalent parameterized matrix" the acceptance criteria allow: it covers **all** routes
rather than a hand-picked list, and it fails on drift in either direction.

## CI

The suite is part of `tests/QuizArena.ArchitectureTests`, which the backend job already runs on every
push and pull request, so the matrix is enforced without a new workflow step.

## Notes for review

- Reading `.cs` sources with two regexes is deliberate: the architecture tests already read project
  files as text and must not reference service assemblies.
- `GET /api/users/{id}` is public in the contract; it is the retired 410 endpoint, so that is correct —
  the matrix now documents it instead of leaving it implicit.
