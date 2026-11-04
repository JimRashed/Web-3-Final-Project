# Milestone 3 testing conventions

Status: introduced by #24 (P09). Applies to every service test project.

## Test layers

| Project | What it may use | What it must not do |
|---|---|---|
| `tests/QuizArena.<Service>.UnitTests` | xUnit, FluentAssertions, Moq | Touch a database, an HTTP host or a real identity provider |
| `tests/QuizArena.<Service>.IntegrationTests` | `WebApplicationFactory`, SQL Server, real `JwtBearer` | Bypass an authorization policy or reuse another test's database |
| `tests/QuizArena.ArchitectureTests` | Roslyn-free project inspection | Reference a service project |
| `tests/QuizArena.TestSupport` | Test-only helpers | Reference a service project or expose a "skip validation" switch |

## Database lifecycle

- Every relational test class owns a **uniquely named** SQL Server database
  (`QuizArena<Service>_<Purpose>_<guid>`), created with `MigrateAsync()` in `InitializeAsync`.
- `DisposeAsync` drops exactly that database. No test truncates or deletes a developer database.
- `QUIZARENA_TEST_SQLSERVER` overrides the LocalDB default for machines or CI agents that expose
  another server.

## Token fixture

`EphemeralTokenIssuer` generates one RSA key per suite and publishes matching
`OpenIdConnectConfiguration` metadata. `AuthenticatedHostFactory<TProgram>` feeds that metadata to
the production `JwtBearer` handler, so signature, issuer, audience, lifetime and `typ` checks all
run for real.

Token shape (mirrors the realm from #20):

| Claim | User token | Machine token |
|---|---|---|
| `sub` | player subject | service-account subject |
| `azp` | `quizarena-client` | `quizarena-gameplay` |
| `qa_actor` | `user` | `machine` |
| `scope` | requested scopes | requested scopes |
| `typ` | `Bearer` | `Bearer` |

Negative controls that every secured service should cover:

| Case | Expected |
|---|---|
| no `Authorization` header | `401` |
| malformed token | `401` |
| token signed with a different key, same `kid` | `401` |
| expired token / token without `exp` | `401` |
| wrong issuer / wrong audience | `401` |
| `typ=ID` (ID token used as access token) | `401` |
| valid token missing the required scope | `403` |
| valid token with the required scope | `200` |

## Naming

`MethodName_Scenario_ExpectedBehavior`, Arrange / Act / Assert, one behaviour per test. No test
may depend on another test's execution order.
