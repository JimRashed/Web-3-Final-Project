# Issue #21 (P06) plan: real JWT validation and reusable scope policies in all six APIs

Parent #8 (F00), epic #7. Prerequisites: P03 #18 (service hosts, merged in #72) and P05 #20 (Keycloak realm, merged in #71).

## Goal
Each of the six Api hosts validates Keycloak access tokens with framework JwtBearer and enforces the named, actor-aware scope policies from docs/milestone3/authorization-matrix.md, with an authenticated fallback, public `/health`, CORS for the React origin and ProblemDetails 401/403.

## File changes (per service S, generated identically)
| Path | Change |
|---|---|
| src/QuizArena.S.Api/QuizArena.S.Api.csproj | Add Microsoft.AspNetCore.Authentication.JwtBearer 10.0.11 (same release as existing ASP.NET packages) |
| src/QuizArena.S.Api/Security/*.cs | Options, claim names, actor record + extensions, scope requirement/handler, `SPolicies`, `SecurityExtensions` |
| src/QuizArena.S.Api/Program.cs | ProblemDetails, controllers, security registration and middleware order |
| src/QuizArena.S.Api/appsettings.json | New base config (Authentication audience/client IDs, blank authority, empty CORS) |
| src/QuizArena.S.Api/appsettings.Development.json | Local Keycloak authority, HTTP metadata allowed, React origin |
| tests/QuizArena.S.IntegrationTests/Security/*.cs | Token factory (ephemeral RSA), secured factory with local metadata, probe controller, security matrix |
| docs/milestone3/authentication.md | API validation section, usage example, evidence |

No shared assembly (ADR-01, architecture tests require each test project to reference only its own Api).

## Validation
- `dotnet build QuizArena.sln --no-restore` (no new warnings; legacy Pkcs advisories unchanged)
- `dotnet format QuizArena.sln --verify-no-changes --no-restore`
- `dotnet test QuizArena.sln --no-build` (architecture + six security matrices)
- Mutation check: break scope matching / audience / typ check, confirm failures, restore
- Live smoke: Statistics API in Development against running Keycloak with real tokens

## Out of scope
Business endpoints and ownership rules (each service's T04), Swagger (T04s), React auth (P07 #22).
