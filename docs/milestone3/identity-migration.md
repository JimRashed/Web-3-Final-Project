# P02 identity and data migration contract

Proposed, not executed. [Architecture](architecture-decisions.md) and [API](api-contracts.md) define the target. Existing QuizArenaDb and credential data remain untouched by this issue.

## Mapping ledger

Identity key is exact (Issuer, Subject). Subject is opaque, case-sensitive, nonblank up to200 characters; issuer is the exact configured authority. Never lowercase/parse subjects or automatically link by username/email. One issuer is trusted per deployment; API userId paths refer to that issuer's subject. Internal keys and each imported service record retain issuer context to avoid collisions if authority changes.

Reviewed ledger fields: migrationRunId, sourceTable/sourceKey, legacyUserId/raw string, targetIssuer, targetSubject, internalProfileId(Guid), proofReference, verifiedBy, verifiedAt UTC, state Verified/Quarantined, reason, sourceChecksum. A mapping must have independent account ownership evidence (verified IdP subject and authenticated recovery/administrator-reviewed provenance). Do not invent a real mapping from example data.

Synthetic example only:

| Legacy reference | Issuer | Subject | Disposition |
|---|---|---|---|
| 8f7d2a15-2e7a-4a7d-8c3a-0f4e4d3f9a21 | configured authority | player-a | Verified only after ownership evidence |
| player-b-old | configured authority | — | Quarantined until proven |

Unmapped, duplicate, ambiguous, conflicting or unverified references remain quarantined with original data. Never silently drop them or associate on a matching display name. Operator resolves ledger conflicts before dependent rows transfer. Same source identity may have many references but one verified target; merging multiple source accounts into one subject requires explicit audited approval and deduplication analysis.

## Password and endpoint transition

Keycloak owns registration, sign-in, recovery and password reset. No assumed password-hash import compatibility. Existing users establish/recover IdP accounts through verified recovery; a username match is insufficient. Old hand-built HMAC JWTs are never accepted by the new APIs. Existing password hashes remain in restricted migration archives under retention/access policy, never copied into new profile tables or public DTOs.

POST /api/auth/register and /api/auth/login return410 safe ProblemDetails pointing to configured OIDC login. Ignore bodies rather than bind/hash/log credentials; do not redirect to caller-supplied URLs. GET /api/users/{id} returns410; new GET/PUT /api/users/me resolves claims. This transition lasts through Milestone3; later removal requires documented approval. Client has no QuizArena password form.

## Ordered transfer and reconciliation

1. Inventory all ten existing tables, reference types, row counts, checksums, timestamps, unique keys, aggregate scores and unresolved mappings. Back up QuizArenaDb and perform a restore rehearsal to an isolated location before cutover.
2. Drain active lobbies/matches to completion before freezing writes. If not feasible, archive them as legacy interrupted state with owner approval; do not resume old mock-question matches as new five-round games. Preserve records and explain exclusions in reconciliation.
3. Freeze legacy writes and capture final consistent snapshot/change cutoff. Never run both old and new writable paths. Create separate service databases with independently owned migrations; retain original QuizArenaDb.
4. Verify ledger, then import owned rows idempotently using source key + migrationRunId provenance. Profile internal Guid may be retained where unique; Subject comes from verified ledger. Apply same mapping to Statistics user IDs, Lobby host/members, Gameplay participants, Notification recipients/preferences. Cross-service references have no database FKs.
5. Match IDs become strings: format old GUID results consistently as lowercase canonical UUID text; preserve existing string match IDs verbatim, including non-UUID values. Detect collisions before import. Do not guess that unrelated result/match IDs refer to the same match. Preserve source keys and quarantine ambiguous associations.
6. Preserve historical scores above5, wins, original PlayedAt/createdAt and event payloads. New result ingestion has score0..5, but historical read schemas allow nonnegative historical values. Imported legacy events lacking EventId receive a deterministic migration mapping stored once; never replay imports as fresh user notifications or new outbox deliveries.
7. Reconcile per table and per account: counts imported+quarantined=source, key uniqueness, mapped references, aggregate totalScore/wins/gamesPlayed, exact timestamps/IDs, sample payloads and checksum evidence. Validate login/profile ownership and self-only access in isolated target before traffic switch.
8. Owners approve reconciliation and restore evidence; configure frontend and all API origins/issuer together, switch traffic once, observe errors and outbox progress. No source deletion. Retain backups/ledger according to agreed retention policy.

Before target accepts writes, rollback restores routing to the frozen original after confirming no target mutations. After target writes, never simply point to stale legacy data: freeze target writes, snapshot all targets/outboxes, reconcile new changes into an approved compatible restoration or fix forward. Human owners choose recovery based on evidence; no automated rollback discards new results/events.

## Required future implementation evidence

P04 and each Data task must demonstrate repeatable migration into empty databases, replay without duplicates, failed mapping quarantine, no cross-service ownership queries/FKs, and rollback rehearsal. P05/P06 and service HTTP tasks demonstrate issuer/audience/scope enforcement with real provider tokens. P02 supplies specification only; no database or credential operation was performed.
