# Issue #140 · SEC-B1 bearer validation

## Summary and architecture

Use the existing JWT bearer pipeline to enforce the configured Keycloak realm issuer, exact host audience, signature, and lifetime for Identity, Statistics, Lobby, Gameplay, Notification, and QuestionBank. Resolve caller identity through the validated principal and preserve the existing policy, challenge, public health, and retired route contracts. The implementation belongs in API security configuration. HTTP integration tests use the existing host fixtures and policy probes.

Start from `feature/milestone-4-security-and-cross-device-profiles`, which contains the security contract merged in #149. [Issue #140](https://github.com/JimRashed/Web-3-Final-Project/issues/140) · [Prerequisite #137](https://github.com/JimRashed/Web-3-Final-Project/issues/137)

## File inventory

| Host | Bearer configuration | HTTP integration coverage |
| --- | --- | --- |
| Identity | `src/QuizArena.Identity.Api/Security/SecurityExtensions.cs` | `tests/QuizArena.Identity.IntegrationTests/Security/SecurityMatrixTests.cs` |
| Statistics | `src/QuizArena.Statistics.Api/Security/SecurityExtensions.cs` | `tests/QuizArena.Statistics.IntegrationTests/Security/SecurityMatrixTests.cs` |
| Lobby | `src/QuizArena.Lobby.Api/Security/SecurityExtensions.cs` | `tests/QuizArena.Lobby.IntegrationTests/Security/SecurityMatrixTests.cs` |
| Gameplay | `src/QuizArena.Gameplay.Api/Security/SecurityExtensions.cs` | `tests/QuizArena.Gameplay.IntegrationTests/Security/SecurityMatrixTests.cs` |
| Notification | `src/QuizArena.Notification.Api/Security/SecurityExtensions.cs` | `tests/QuizArena.Notification.IntegrationTests/Security/SecurityMatrixTests.cs` |
| QuestionBank | `src/QuizArena.QuestionBank.Api/Security/SecurityExtensions.cs` | `tests/QuizArena.QuestionBank.IntegrationTests/Security/SecurityMatrixTests.cs` |

Create this plan. Reuse each project's existing `SecuredApiFactory` and extend its existing `tests/QuizArena.<Service>.IntegrationTests/Security/TestTokens.cs` with a synthetic credential in the retired host's format. Run the existing Identity retired-route coverage in `tests/QuizArena.Identity.IntegrationTests/Endpoints/ProfileEndpointsTests.cs`. Align `docs/milestone3/authorization-matrix.md` and `docs/milestone3/security-decisions.md` with the shipped machine policy to resolve DR-1.

## Ordered steps

1. Run the baseline build and targeted security matrices. Inspect each host's configured audience, `ConfigureJwtBearer`, fixture overrides, and existing claim, challenge, health, and retired-route assertions. Record any baseline failures.
2. Configure all six hosts to compare the token issuer ordinally against the configured authority normalized for its optional trailing slash. Preserve the framework's retrieval and refresh of metadata signing keys, required signed RS256 access tokens and expiration, lifetime validation, and existing clock skew. Require exact audience matching by disabling trailing-slash tolerance. A valid token with several audiences still passes when one is the host's exact audience, as required by SEC-12.
3. Extend each host's security matrix with a real peer service audience that is rejected, a token whose audiences include the host and a peer that is accepted, and a legacy credential that cannot authenticate a protected request. Cover exact issuer and audience matching with the local signing fixture. Use the matrix's existing claim-mutation helper and assert the response through the real bearer handler. For cross-service tests, use an otherwise valid token and a different configured QuizArena service audience.
4. Run the targeted checks and full solution gates. Record results for each host, confirm the bearer challenge for missing and rejected credentials, and verify public health and Identity's retired endpoints. Review the final diff against all five acceptance criteria.

## Acceptance coverage

| Issue requirement | Verification |
| --- | --- |
| Trusted Keycloak issuer, service-specific audience, valid signature and lifetime via existing bearer setup | Steps 1–3. All six production configurations use the existing handler. Matrices assert successful authentication and rejection for mismatches, invalid signatures, and expired tokens. |
| Identity from validated claims | Steps 1 and 4. Existing matrix fixtures verify the caller subject and issuer come from the principal while untrusted identity inputs are ignored. |
| Reject legacy credentials and preserve public and retired routes | Steps 3–4. Legacy requests get 401 on protected probes. Existing public health tests return 200 and Identity's retired routes return 410, including the existing invalid-bearer case. |
| Deterministic fixture evidence for valid, missing, malformed, expired, wrong issuer, wrong audience, and invalid signature | Steps 1, 3, and 4. Run all six matrices with isolated test signing keys and expiry values safely beyond the configured clock skew. |
| Cross-service audience rejection and bearer challenge | Steps 3–4. Every host rejects a peer-only audience and accepts its own audience in a valid multi-audience token. A missing bearer credential yields the bare Bearer challenge. A rejected bearer token yields the configured `invalid_token` challenge. |

## Testing and validation

Use xUnit and FluentAssertions in the existing HTTP integration matrices. Supply only synthetic tokens through the existing OIDC fixture. Confirm the fixtures retain the host's JWT validation settings and replace only discovery with local issuer metadata and signing keys. Application policy probes exercise the real authentication middleware without a live realm. The full solution suite includes relational tests that require LocalDB access.

From the worktree root:

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror
dotnet test QuizArena.sln --no-build --filter 'FullyQualifiedName~SecurityMatrixTests|FullyQualifiedName~RetiredRoute' --verbosity normal
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test QuizArena.sln --no-build --verbosity normal
git diff --check
```

Record exit codes and test counts by host. The relational suites use isolated databases on LocalDB. If LocalDB cannot start, set the documented `QUIZARENA_TEST_SQLSERVER` environment variable for the test process to use an existing local SQL Server whose test account can create isolated databases. Review the production settings across all six hosts and verify the fixture leaves the authentication scheme and production authorization policies in place.

## Risks and scope

- The milestone contract deliberately allows browser access tokens that carry several service audiences. Each host still requires its own exact audience and applies its existing policy.
- Resolve DR-1 through its documented matrix update option. The imported realm does not pin a stable machine service-account subject. The machine rules keep the trusted realm's signed client and actor claims, opaque subject and exact scope requirements. #146 tracks live verification.
- Scope and ownership changes, live Keycloak verification for Gameplay service-to-service calls, and profile picture work are tracked separately. This plan changes no public route declarations, endpoint semantics, OpenAPI definitions, or realm configuration.
