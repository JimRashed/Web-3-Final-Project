# Issue #26 (S01-T01) plan: Identity domain model and pure rules

Parent F01 #9, epic #7. Prerequisite P03 #18 (merged in #72).

## Goal
A pure `PlayerProfile` entity keyed by trusted issuer + opaque subject with a bounded display name, and no credential/token logic (Keycloak owns sign-in).

## Files
| Path | Change |
|---|---|
| src/QuizArena.Identity.Domain/Entities/PlayerProfile.cs | Entity: Create / Rehydrate / Rename / BelongsTo / SuggestDisplayName |
| src/QuizArena.Identity.Domain/Exceptions/IdentityDomainException.cs | Base domain exception |
| src/QuizArena.Identity.Domain/Exceptions/InvalidProfileException.cs | Field-level invariant violation (maps to 400) |
| tests/QuizArena.Identity.UnitTests/Domain/PlayerProfileTests.cs | Unit tests for every rule and boundary |

## Rules (from docs/milestone3/api-contracts.md and identity-migration.md)
- Issuer: required, absolute http(s) URI, at most 256 characters, exact (no trimming).
- Subject: required, at most 200 characters, opaque and case-sensitive; rejected (not trimmed) if padded.
- Display name: trimmed, 1..80 characters, case preserved, no control characters.
- Id: non-empty Guid, stable across renames. Timestamps UTC; updatedAt >= createdAt.
- Rename is idempotent: same normalized name returns false and keeps updatedAt.
- `SuggestDisplayName(preferred_username)` gives a valid prefill or "Player"; never an identity key.

## Validation
`dotnet test tests/QuizArena.Identity.UnitTests`, architecture tests (Domain has no packages/references), `dotnet format --verify-no-changes`.

## Out of scope
Persistence and repository ports (#27), provisioning flow / GET /me behavior (#28, #29), legacy password archive (D02 #63).
