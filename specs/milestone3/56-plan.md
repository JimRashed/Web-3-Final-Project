# #56 — Notification domain models and pure business rules

> Issue: #56 (S06-T01) · Feature: #14 (F06) · Epic: #7 (E00) · Owner: @SuSu-Lisu
> Contract of record: `contracts/openapi/notification.yaml` (P02, proposed).
> This plan covers the #56 domain layer and sketches the remaining S06 tasks (#57–#61)
> so the slice can proceed in dependency order.

## Scope

| Issue | Layer | Deliverable |
|---|---|---|
| #56 | Domain | Entities, enums, exceptions, pure rules |
| #57 | Data | DbContext, EF configurations, migrations, repositories |
| #58 | Api | DTOs + application orchestration |
| #59 | Api | Endpoints, bearer auth, policies, OpenAPI |
| #60 | Client | React screens + REST integration |
| #61 | Tests | Unit + integration coverage, regression |

## Target layout (per ADR-01/ADR-02)

```
src/QuizArena.Notification.Domain/     Entities/ Enums/ Exceptions/ Services/
src/QuizArena.Notification.Data/       Context/ Configurations/ Repositories/ Migrations/
src/QuizArena.Notification.Api/        Controllers/ Contracts/ Services/ Program.cs appsettings.json
src/QuizArena.Client/src/features/notification/
tests/QuizArena.Notification.UnitTests/
tests/QuizArena.Notification.IntegrationTests/
```

Dependency direction: `Api → Data → Domain`. Domain has zero EF/ASP.NET/NuGet dependencies.

## Domain model (#56)

**Event** (immutable log entry)
- `EventId : Guid` — supplied by producer, unique per issuer
- `Type : string` — e.g. `MATCH_COMPLETED`, `MATCH_RESULT`
- `UserId : string` — opaque subject from the validated token
- `PayloadJson : string` — canonical payload, compared verbatim on replay
- `CreatedAt : DateTime` (UTC, set once)

**UserPreference**
- `Issuer`, `Subject` — unique pair, never parsed as GUID
- `EmailEnabled`, `PushEnabled` (default `true`)

### Invariants
1. Events are append-only; no update/delete path exists.
2. Same `EventId` + identical payload → return the original record (idempotent retry).
3. Same `EventId` + different payload → `409 Conflict`, nothing written.
4. Preferences default to `true/true` when the user has no row; update upserts.
5. Identity always comes from the token subject — never from a request body or header.

## Data layer (#57)

- `NotificationDbContext` owned by this service only.
- Fluent configs: unique index on `Events.EventId`; unique index on `UserPreferences (Issuer, Subject)`.
- Migration `InitialCreate` under `Migrations/`.
- Repositories: `IEventRepository`, `IUserPreferenceRepository` (interfaces in `Domain`, implementations in `Data`).
- Reads use `AsNoTracking()`; all I/O async with `CancellationToken`.

## API layer (#58, #59)

Operations from the contract:

| Method | Route | Policy | Actor |
|---|---|---|---|
| POST | `/events` | `events.write` | machine |
| GET | `/events` | `events.read` | player (own events) |
| GET | `/users/{userId}/preferences` | `preferences.read` | player (self only) |
| PUT | `/users/{userId}/preferences` | `preferences.write` | player (self only) |
| GET | `/health` | public | – |

- JWT bearer validation against the Keycloak realm; audience `quizarena-notification`; issuer configurable per environment.
- `/{userId}` must equal the caller's subject → otherwise `403`.
- Thin controllers: HTTP concerns only, orchestration in application services.
- Responses are DTOs, never entities.

## Client (#60)

- `features/notification/`: event list, preferences form with email/push toggles.
- Talks to the API over HTTP only; loading/error/empty states.
- Config via `VITE_NOTIFICATION_BASE_URL`, `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID`.

## Tests (#61)

**Unit** — idempotent replay returns original; conflicting payload throws; defaults applied; upsert semantics.
**Integration** — no token → `401`; wrong scope → `403`; valid scope → `200/202`; `/health` public; machine token rejected on player route.

## Execution order

`#56 → #57 → #58 → #59 → #61 → #60` (client last, after the contract is live).

## Open questions

1. Exact `GET /events` route (`/events` vs `/events/me`) — confirm against `docs/milestone3/api-contracts.md`.
2. Whether `POST /events` returns `202` with a body (contract shows `202` + `Event`).
3. Scaffold naming from #18 — if the service project names differ, adjust paths here before coding.
