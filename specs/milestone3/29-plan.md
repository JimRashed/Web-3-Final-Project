# Issue #29 (S01-T04) plan: Identity REST endpoints with OpenAPI

Parent F01 #9, epic #7. Prerequisites #28 (application layer, merged in #84) and P06 #21 (JWT policies, merged in #73).

## Files
| Path | Change |
|---|---|
| src/QuizArena.Identity.Api/Controllers/ProfilesController.cs | GET/PUT /api/users/me (profiles.read / profiles.write); retired GET /api/users/{id} → 410 |
| src/QuizArena.Identity.Api/Controllers/RetiredAuthController.cs | POST /api/auth/register and /login → 410, no body binding |
| src/QuizArena.Identity.Api/Services/IdentityExceptionHandler.cs | InvalidProfileException → 400 ValidationProblemDetails; aborted requests are not reported as 500 |
| src/QuizArena.Identity.Api/Program.cs, *.csproj | Reject unknown JSON members, exception handler, OpenAPI (Microsoft.AspNetCore.OpenApi 10.0.11) mapped in Development only |
| tests/QuizArena.Identity.IntegrationTests/Endpoints/*.cs | Real-pipeline HTTP tests with their own migrated database |

## Behavior (contracts/openapi/identity.yaml, authorization-matrix.md)
- GET /me: 200 profile or 404 problem until the player saves a name. PUT /me: 200, upsert, body `{displayName}` only.
- Unknown body properties (e.g. `userId`) and malformed JSON → 400; nothing is written.
- Retired routes are public, ignore any body and bearer header, and always return 410 with OIDC guidance.
- 401/403 come from the P06 policies (scope + user actor).
- `/openapi/v1.json` exists only in Development and lists exactly the contract operations.

## Validation
`dotnet build -warnaserror`, `dotnet test tests/QuizArena.Identity.IntegrationTests`, `dotnet format --verify-no-changes`, architecture tests, no leftover test databases.
