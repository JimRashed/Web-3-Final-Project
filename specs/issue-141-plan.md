# Issue 141 — Scope, actor, and ownership checks

[SEC-B2](https://github.com/JimRashed/Web-3-Final-Project/issues/141) · Agent-reviewed implementation plan · Human review pending

## Summary and architecture

Reconcile the shipped Identity, Statistics, Lobby, Gameplay, Notification, and QuestionBank endpoints with the [authorization matrix](../docs/milestone3/authorization-matrix.md). Each API keeps its existing bearer validation, exact scope policy, and signed browser or Gameplay client and actor classification. API input handling validates syntax and binds only declared fields. Application and domain rules authorize resource access using the validated actor before writes. Persisted denials are verified against SQL Server with fresh contexts.

Use `feature/milestone-4-security-and-cross-device-profiles` at `b37b8c1`, which descends from the issue's listed baseline `c8dcc78` and includes prerequisites [#137](https://github.com/JimRashed/Web-3-Final-Project/pull/149) and [#140](https://github.com/JimRashed/Web-3-Final-Project/pull/152). The #137 maintainer merged the matrix to unblock dependent work and recorded JimRashed's approval and SuSu-Lisu's outstanding approval. Apply the merged rules for shipped routes. [#137 closure evidence](https://github.com/JimRashed/Web-3-Final-Project/issues/137#issuecomment-7074591731)

## File inventory

| Files | Changes |
| --- | --- |
| `src/QuizArena.Gameplay.Api/Services/GameplayApplication.cs` | Validate the six-character ASCII uppercase letter/digit join code against the existing contract. Check the verified lobby host before match lookup or lock-state checks. Keep repeated starts and concurrent winner reads idempotent for the host. |
| `src/QuizArena.Gameplay.Api/Errors/GameplayExceptionHandler.cs` | Set canonical title and safe nonempty detail for authorization denials mapped to 403. Preserve other error handling. |
| `src/QuizArena.Lobby.Api/Errors/LobbyProblems.cs` | Set the canonical title and detail in the existing 403 mapping for bearer and resource authorization. |
| `src/QuizArena.Notification.Api/Controllers/EventsController.cs` | Route the legacy `userId` mismatch through the existing bearer forbid response. Keep paging validation first and pass only actor issuer and subject to the application. |
| `src/QuizArena.Identity.Api/Controllers/ProfilesController.cs`, `src/QuizArena.Identity.Api/Controllers/RequireJsonContentTypeAttribute.cs`, `contracts/openapi/identity.yaml`, `contracts/validate-contracts.mjs` | Validate the profile PUT content type after authorization, preserve its request metadata, and declare the observed 415 problem response in the committed contract and checker. |
| `tests/QuizArena.Gameplay.IntegrationTests/Application/GameplayApplicationTests.cs`, `tests/QuizArena.Gameplay.IntegrationTests/Errors/GameplayExceptionHandlerTests.cs` | Cover the host-first decision, input validation before a peer call, and the safe canonical 403 exception mapping. |
| `tests/QuizArena.Gameplay.IntegrationTests/Endpoints/GameplayApiFactory.cs`, new `tests/QuizArena.Gameplay.IntegrationTests/Endpoints/GameplayRouteSecurityTests.cs` | Add an opt-in real SQL database to the existing bearer-configured factory while keeping peer ports as test doubles. Cover real route grants, ownership, persisted match/answer/delivery state, and authorized requests. |
| `tests/QuizArena.Notification.IntegrationTests/Endpoints/NotificationApiFactory.cs`, new `tests/QuizArena.Notification.IntegrationTests/Endpoints/NotificationRouteSecurityTests.cs`, `tests/QuizArena.Notification.IntegrationTests/Endpoints/NotificationEndpointsTests.cs` | Add an opt-in real SQL database using the existing bearer host and the real event application. Cover real route grants, ownership, persisted event/preference state, and the canonical legacy-query denial. |
| `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyPrincipalIsolationTests.cs`, `tests/QuizArena.Lobby.IntegrationTests/Endpoints/LobbyApiFixture.cs` | Assert the canonical 403 body for ownership and grant denials through the helper shared with `LobbyRouteSecurityTests`. Verify a different issuer with the same subject cannot act as the host of a stored lobby. |
| `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileEndpointsTests.cs` | Verify authorization precedes media type validation, the 401/403/415 responses leave profile storage untouched, and OpenAPI advertises `application/json` requests. |
| `docs/milestone3/authorization-matrix.md`, `docs/milestone3/security-decisions.md`, `docs/milestone3/architecture-decisions.md`, `docs/milestone3/api-contracts.md` | Reconcile check order, denial body, 415, and the Lobby and Gameplay single-issuer ownership exception. Record test evidence for all six services. |

## Ordered implementation steps

1. **Confirm the baseline.** At `b37b8c1`, Dispatcher recorded successful restore and build with zero warnings and errors, 1,542 passing tests, and a successful contract check covering 32 operations and 22 probes. Audit actual controller policies and DTO binding against each protected route row, including all four machine-only routes. Keep existing rules that already match and distinguish baseline evidence from the post-change run.

2. **Enforce Gameplay match creation order.** Validate join-code syntax before the Lobby gateway, returning 400 for invalid input and preserving 404 for a valid but unknown code. Check that the authenticated subject equals the verified host with ordinal comparison immediately after resolving the lobby. An existing lobby requested by a non-host returns 403 whether open, locked, or already associated with a match. An authorized repeat returns the existing match with 200. The host can create a match only for a locked two-member lobby and receives 201 on creation. Preserve the post-race re-read and answer concurrency. The application regression tests should distinguish non-host 403 from host 409 for the same unlocked state and verify invalid code never reaches the gateway.

3. **Align denial bodies and the Identity media-type contract.** Reuse the production bearer forbidden path for Notification's legacy event lookup mismatch. Align Lobby and Gameplay ownership denials with the title `Insufficient scope, actor kind or ownership`. For 403 check `application/problem+json`, status, title, nonempty safe detail, type, and trace ID, with no resource data or challenge header. Statistics already uses the canonical title and a safe, more specific detail. For profile PUT, enforce `application/json` in an MVC resource filter after scope and actor checks and before model binding. Verify the 401/403/415 responses with the real MVC pipeline and use its actual 415 fields in OpenAPI.

4. **Exercise the real authorization and persistence boundaries.** Keep test-issued JWTs and metadata local to the integration host, with production authentication and policies enabled. Add focused route matrices for Gameplay's four user routes and Notification's three user routes plus its machine ingress. With the correct audience, assert that opposite actors with the exact route scope are forbidden and eligible actors with a near-match scope are forbidden. For Gameplay also cover unknown resources, outsider GET, next-question and answer requests, repeated non-host starts, invalid join codes, and rejected identity fields on create and answer. Exercise both Lobby and Gameplay using a same-subject token from another issuer, which must fail before persistence or ownership handling. For Notification verify no delivery or preferences can be created or changed by a denied actor, body field, or legacy query/path user ID. Include allowed host and participant operations, existing default preference behavior, and an allowed machine event write as controls.

   Reuse the existing test factory configurations and enable real SQL in the new suites only, following `GameplayRaceTests` and `NotificationRepositoryTests` for unique test databases, migrations, disposal and the `QUIZARENA_TEST_SQLSERVER` override. Use separate no-tracking contexts to compare meaningful before/after persisted snapshots. Compare Gameplay matches, stored answers and outbox records, including zero rows after denied creation, and Notification events and both players' preferences, including the absence of a row where appropriate. Keep the Gameplay outbox worker disabled for deterministic persisted evidence. Existing fixtures remain isolated from the new SQL cases.

5. **Reconcile and verify the matrix.** Document the host-before-state check, standardized 403 response, and shipped Identity 415 by resolving DR-2, DR-4 and DR-9. Record DR-5's allowed single-issuer exception in the matrix, decision register, and ADR-05. Lobby and Gameplay store ordinal subject keys inside issuer-private databases and run against the same exactly pinned issuer. Reusing either store with a different issuer requires intentional migration or a new store. Keep schema and peer data contracts intact. Link each service to its real-route and persistence evidence. For QuestionBank verify denied reads preserve the question catalog and withhold snapshots. Preserve all documented success codes, authentication challenges and public exceptions.

## Acceptance coverage

| Criterion | Verification |
| --- | --- |
| Reconcile enforcement with the matrix across all six services | Steps 1 and 5. Cross-reference exact policies and actors in the matrix, six real-route suites, and the contract validator. |
| Exact scopes and distinct user and Gameplay machine identities | Steps 1 and 4. Real-route near-match denial and exact-scope success for the appropriate actor. Preserve signed `azp`, `qa_actor`, nonblank `sub`, and exact issuer/audience checks. |
| Browser tokens denied for machine routes and machine tokens denied for user routes | Step 4. Token bears the correct destination, route scope, and opposite actor, so the result is an actor denial. |
| Claim-based self access, host checks, and participant checks | Steps 2–5. Identity issuer/subject self, Statistics stats/history self, Notification event and preference self, Lobby lock owner, Gameplay verified host and match participant. Cross-issuer same-subject requests are denied at the issuer boundary. |
| Client input cannot select the acting identity | Steps 2–4. Assert injected actor fields fail validation and path/query IDs cannot override the principal. Machine-ingress player/recipient IDs remain business targets. |
| Agreed denial statuses and bodies | Steps 2–4. Cover 401 challenge, canonical 403, syntactic/body 400, missing resource 404, incompatible media 415, and host business-state 409. |
| Denied mutations preserve state and authorized behavior | Steps 2–5. Assert persisted snapshots with new contexts, including matches, deliveries, lobby state, events, preferences and profiles. Reuse existing Statistics result and QuestionBank catalog state tests. Run allowed route baselines and the full suite. |

## Testing strategy

Use xUnit and FluentAssertions in existing test projects. Reuse `ProfileRouteSecurityTests`, `StatisticsRouteSecurityTests`, `LobbyRouteSecurityTests`, `LobbyPrincipalIsolationTests`, `QuestionRouteSecurityTests` and `QuestionSqlPrincipalIsolationTests`. Retain existing authorized HTTP behavior coverage in each service. Add narrowly scoped tests around changed rules and use the Gameplay application/error tests for precedence and response mapping independent of SQL. Keep database checks relational and fixture databases disposable. Assert 401 challenges and problem contracts through HTTP. Use local test fixtures only.

## Validation commands

Run from the solution root with .NET 10, Node, and LocalDB or a trusted local SQL Server available through `QUIZARENA_TEST_SQLSERVER`. Record exit codes, warnings, test totals, and any baseline blockers. Use process-level SQL configuration and omit connection details from published logs.

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --filter 'FullyQualifiedName~RouteSecurityTests|FullyQualifiedName~PrincipalIsolationTests|FullyQualifiedName~GameplayApplicationTests|FullyQualifiedName~GameplayExceptionHandlerTests|FullyQualifiedName~ProfileEndpointsTests|FullyQualifiedName~NotificationEndpointsTests|FullyQualifiedName~MatchesEndpointTests' --verbosity normal
dotnet test QuizArena.sln --no-build --verbosity normal
node contracts/validate-contracts.mjs
git diff --check
```

## Risks and exclusions

- An issuer change affects the meaning of opaque subject IDs in Lobby and Gameplay. The exception applies only to deployments whose two services pin the same issuer and keep their persisted stores private to that issuer.
- Keep historical outage mappings (DR-3 and DR-8), CI matrix expansion (#142), picture delivery (#144), and Gameplay downstream credentials and peer contracts (#146) in their own scopes.
- Keep public and retired routes, token validation from #140, and production persistence schemas intact. The only requested publication gate is independent human review of the validated draft PR.
