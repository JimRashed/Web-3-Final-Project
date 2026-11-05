# Issue #44: Lobby domain rules

## Summary and architecture

Issue [#44](https://github.com/JimRashed/Web-3-Final-Project/issues/44), parent [#12](https://github.com/JimRashed/Web-3-Final-Project/issues/12), epic #7. Work takes place on `feature/m3/12-lobby-service`, based on milestone commit `4f5d8cb`, and joins the single parent PR. This is an agent-reviewed plan; human signoff remains pending.

Implement pure owned Lobby entities and rules in `QuizArena.Lobby.Domain`. The existing owned Domain and unit project are empty. Legacy aggregate Lobby entities still serve the aggregate context, repositories and API, so retain them until the separately scoped retirement work. The owned Domain must have zero package/framework/project/assembly dependencies. HTTP, EF, repositories and trusted-principal extraction belong to subsequent tasks.

## Exact file inventory

Create:

- `src/QuizArena.Lobby.Domain/Entities/ActiveLobby.cs`: encapsulated lobby aggregate, creation, checked rehydration, membership and state transitions.
- `src/QuizArena.Lobby.Domain/Entities/LobbyPlayer.cs`: immutable membership identity, lobby association and opaque player subject.
- `src/QuizArena.Lobby.Domain/Enums/LobbyStatus.cs`: `Open` and `Locked` only.
- `src/QuizArena.Lobby.Domain/Exceptions/LobbyDomainException.cs`: common domain exception base.
- `src/QuizArena.Lobby.Domain/Exceptions/InvalidLobbyException.cs`: invalid identifier/code/state input, with field context.
- `src/QuizArena.Lobby.Domain/Exceptions/LobbyConflictException.cs`: capacity or transition conflict.
- `src/QuizArena.Lobby.Domain/Exceptions/LobbyForbiddenException.cs`: nonhost lock attempt.
- `src/QuizArena.Lobby.Domain/Services/JoinCodeGenerator.cs`: six-character ASCII uppercase alphanumeric candidate generation and validation using the BCL.
- `tests/QuizArena.Lobby.UnitTests/Domain/ActiveLobbyTests.cs`: domain creation, rehydration and transition cases.
- `tests/QuizArena.Lobby.UnitTests/Domain/JoinCodeGeneratorTests.cs`: output shape and invalid code cases.

Modify only this scoped plan if implementation needs a cohesive filename adjustment. No project references, packages, legacy files, owned persistence/API files or frontend files change in this child.

## Ordered steps

1. Define the two states and semantic typed exceptions. Invalid input is distinct from state conflict and host authorization; subsequent Api mapping will translate these to 400/409/403. Completion: Domain builds without outer dependencies.
2. Define controlled membership and aggregate construction. Use nonempty Guid identifiers for lobby/membership, a validated join code, and a host subject that appears exactly once among members. Creation starts Open with the host as its first member. Preserve opaque subjects exactly and compare ordinally, case-sensitively; follow existing Identity identifier validation (nonblank, at most 200 characters, no surrounding whitespace, rejection rather than trimming). Expose membership through an actual read-only view so callers cannot mutate collection contents or membership fields to bypass rules. Completion: valid creation succeeds; invalid inputs and external mutation attempts fail without partial state.
3. Provide explicit checked rehydration suitable for the later persistence mapper. Reject undefined states, zero/duplicate IDs, mismatched membership lobby IDs, missing host, duplicate subjects, roster size outside 1..2, and Locked with fewer than two players. Copy caller collections so later external mutations cannot change the aggregate. Completion: valid stored Open/Locked states restore; corrupt state is rejected before use.
4. Implement `Join` and `Lock` domain operations. Join validates the caller, rejects Locked before duplicate checks, returns unchanged for an existing Open member, accepts a distinct second member, and rejects a third without mutation. Lock validates the caller and checks exact host authorization first, including an already Locked lobby. The host needs exactly two members to transition; repeated host lock returns the existing Locked state. Boolean changed/not-changed results or equivalent explicit results may express idempotency. Completion: every stated transition and ordering rule is covered.
5. Generate six characters from `ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789` using BCL randomness; do not claim uniqueness or implement collision retries here. Validate supplied codes strictly; lowercase, wrong length, non-ASCII, punctuation and whitespace are invalid under the committed public contract. Completion: generation has the required shape; supplied values are never silently uppercased.
6. Add focused xUnit/FluentAssertions tests and run validation. Tests use Arrange/Act/Assert and descriptive scenario names, no external I/O or mock persistence. Inspect the complete diff and pass it to Testing, Validate and read-only Code Review through Dispatcher.

## Acceptance coverage

| Acceptance criterion | Steps and evidence |
|---|---|
| Exactly two distinct subjects including host | 2–4; creation, second/third join, ordinal identity and corrupt roster tests |
| Open → Locked; join only Open | 3–4; invalid state, locked joins for existing/new members |
| Open duplicate join idempotent | 4; roster and identity unchanged |
| Third/Locked join conflicts | 4; typed conflict and no mutation |
| Host alone may lock, exactly two members required | 4; nonhost forbidden, one-member host conflict, successful lock |
| Repeat host lock idempotent | 4; repeated host succeeds unchanged; nonhost remains forbidden |
| Normalized uppercase six-character alphanumeric generation | 5; ASCII format and strict supplied-code validation |
| Correct ownership and pure Domain | 1–6; unchanged project file and architecture tests |
| Actual plan, tests and read-only review | This plan and per-child dispatcher reports; final parent PR links evidence |
| Independent human approval | Pending on the single draft PR; no automated signoff or manual issue closure |

## Tests and validation

Baseline supplied by Dispatcher: restore/build succeeded with zero build warnings/errors; architecture 6 tests passed; the empty owned Lobby unit scaffold discovered zero tests. This is baseline context, not new behavior evidence.

Run from repository root after implementation:

```powershell
dotnet restore QuizArena.sln
dotnet build QuizArena.sln --no-restore -warnaserror -m:1
dotnet format QuizArena.sln --verify-no-changes --no-restore
dotnet test tests/QuizArena.Lobby.UnitTests/QuizArena.Lobby.UnitTests.csproj --no-build --verbosity normal
dotnet test tests/QuizArena.ArchitectureTests/QuizArena.ArchitectureTests.csproj --no-build --verbosity normal
dotnet test QuizArena.sln --no-build --verbosity normal -m:1
git diff --check
```

Record actual exits, discovered/passed/failed/skipped totals and warnings. If sandbox process permissions prevent execution, send the exact command to root for visible execution and inspect its real logs; do not wait on a subagent approval or substitute historical counts. Coverage and aggregate regression evidence also belong to the parent final validation.

## Risks and exclusions

- Domain encapsulation must remain compatible with later persistence through a mapper or explicit EF configuration; do not introduce EF attributes, rowversion or database retry policy here.
- The contract requires one or two players in internal lookup; Gameplay specifically checks Locked and two. Do not require all rehydrated lobbies to be Locked.
- Concurrency and join-code uniqueness are persistence concerns for #45; pure Domain tests establish serial invariant behavior only.
- No leave/unlock/host transfer, player identity rewriting, HTTP status types, endpoint/controller work, direct SQL, package additions, machine-token behavior or legacy retirement.
- Only final independent human review can approve the parent PR. Dispatcher may perform at most two repair cycles for this child and must revalidate/review changed code.
