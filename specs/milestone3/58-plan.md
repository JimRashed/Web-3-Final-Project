# #58 — Notification API DTOs and application orchestration

> Issue: #58 (S06-T03) · Feature: #14 (F06) · Epic: #7 (E00) · Owner: @SuSu-Lisu
> Depends on #56 (domain) and #57 (persistence), both merged.

## Scope

Request/response contracts and the application service that orchestrates them. Controllers,
authorization policies and OpenAPI come with #59.

## Contracts

| Type | Shape |
|---|---|
| `CreateEventRequest` | `{ eventId, type, userId, payload }` — unknown members rejected |
| `CreateEventPayload` | `{ matchId, winnerUserId?, draw? }` — unknown members rejected |
| `EventResponse` | `{ eventId, type, userId, payload, createdAt }` |
| `EventPayloadResponse` | `{ matchId }` for `MATCH_CREATED`; `{ matchId, winnerUserId, draw }` for `MATCH_COMPLETED` (null members omitted) |
| `UserPreferencesResponse` | `{ emailEnabled, pushEnabled }` |
| `UpdatePreferencesRequest` | `{ emailEnabled, pushEnabled }` — unknown members rejected |

`[JsonUnmappedMemberHandling(Disallow)]` is what turns "fields additional to the typed payload"
into a 400 instead of a silently ignored property.

## Application service

`IEventApplication` / `EventApplication` translate the contract into domain calls:

- **Publish** — `MATCH_CREATED` must carry only `matchId`; `MATCH_COMPLETED` requires `matchId`,
  `winnerUserId` and `draw`; anything else is an `InvalidEventException`. The repository's replay
  policy decides new / idempotent / conflict, so the application layer stays thin.
- **GetEvents** — recipient's events, newest first.
- **GetPreferences** — stored values, or the disabled default when the player never saved any.
- **UpdatePreferences** — upsert through the domain factory, return the stored values.

Ownership (caller subject == path subject) stays in the API layer (#59), where the validated token
is available.

## Wiring

`Program.cs` registers the two repositories and the application service as scoped services.
Controllers are not added here.

## Validation

```powershell
dotnet build QuizArena.sln
dotnet test tests/QuizArena.Notification.IntegrationTests --filter EventApplicationTests
```

Nine application tests with mocked repositories: mapping for both event types, rejection of an
outcome on `MATCH_CREATED`, unknown type, missing `matchId`, conflict propagation, preference
defaults, stored preferences and upsert payloads.
