# Issue #28 (S01-T03) plan: Identity API DTOs and application orchestration

Parent F01 #9, epic #7. Prerequisites #26 and #27 (merged), P02 contracts.

## Files
| Path | Change |
|---|---|
| src/QuizArena.Identity.Api/Contracts/ProfileResponse.cs, UpdateProfileRequest.cs | DTOs matching `Profile` / `UpdateProfile` in contracts/openapi/identity.yaml |
| src/QuizArena.Identity.Api/Services/IProfileApplication.cs, ProfileApplication.cs | GetMe / UpsertMe orchestration (contract name `IProfileApplication`) |
| src/QuizArena.Identity.Api/Program.cs | Register `TimeProvider.System` and the application service |
| tests/QuizArena.Identity.IntegrationTests/Application/ProfileApplicationTests.cs | Mocked-repository tests and DTO-vs-contract shape checks |
| tests/QuizArena.Identity.IntegrationTests/*.csproj | Moq 4.20.72 (same version as the unit-test projects) |

## Decisions
- Follows the merged P02 contract where it differs from the older issue text: response `{id, issuer, subject, displayName}`; GET /me returns null → 404 until the player saves a name; PUT upserts. No server-side auto-provisioning on GET.
- The acting player is only ever `AuthenticatedActor` (validated token); DTOs carry no user id. Machine actors are refused.
- Application tests live in IntegrationTests because the architecture tests allow only that project to reference the Api assembly.
- Password hashing / HMAC tokens exist only in the legacy aggregate host; the new Identity service has none (retirement in D02 #63, 410 routes in #29).

## Validation
`dotnet build -warnaserror`, `dotnet test tests/QuizArena.Identity.IntegrationTests`, `dotnet format --verify-no-changes`, architecture tests.
