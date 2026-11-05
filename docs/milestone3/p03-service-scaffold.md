# P03 service scaffold

The six ASP.NET Core .NET 10 hosts implement the [P02 service boundary](architecture-decisions.md). Each service owns a `Domain`, `Data`, and `Api` project and corresponding unit and integration test projects. Its `Api` references only its own `Domain` and `Data`; `Data` references only its own `Domain`; `Domain` has no external package or project dependency. The architecture test project checks this graph and rejects cross-service references.

| Service | Development URL | Launch project |
|---|---|---|
| Identity | `http://localhost:5101` | `src/QuizArena.Identity.Api/QuizArena.Identity.Api.csproj` |
| Statistics | `http://localhost:5102` | `src/QuizArena.Statistics.Api/QuizArena.Statistics.Api.csproj` |
| QuestionBank | `http://localhost:5103` | `src/QuizArena.QuestionBank.Api/QuizArena.QuestionBank.Api.csproj` |
| Lobby | `http://localhost:5104` | `src/QuizArena.Lobby.Api/QuizArena.Lobby.Api.csproj` |
| Gameplay | `http://localhost:5105` | `src/QuizArena.Gameplay.Api/QuizArena.Gameplay.Api.csproj` |
| Notification | `http://localhost:5106` | `src/QuizArena.Notification.Api/QuizArena.Notification.Api.csproj` |

From the repository root, launch one host with `dotnet run --project src/QuizArena.Identity.Api/QuizArena.Identity.Api.csproj --launch-profile QuizArena.Identity.Api`, then request `http://localhost:5101/health`. Substitute the project/profile/port for the other hosts. The `appsettings.Development.json` and launch profile supply development defaults; `--urls` or `ASPNETCORE_URLS` can override the URL. Configure deployed URLs and HTTPS in the deployment environment.

`GET /health` is anonymous and returns only `{ "status": "ok" }` with HTTP 200. It checks that the host is running; it does not query a database or call another service. The hosts have no business routes yet, and their empty Domain/Data libraries are intentional extension points. No credentials, connection strings, or machine identities are supplied by this scaffold.

The existing aggregate `QuizArena.Api`, `.Data`, `.Domain` and tests remain operational during migration. P04 owns per-service EF contexts, configuration and migrations; P06 adds bearer authentication and route policies; the service leaf issues add business behavior; P07 owns the React client; D02 retires the aggregate graph after migration (done in #63; see [data cutover](data-cutover.md)). This P03 change does not copy legacy endpoints or attach the shared `QuizArenaContext` to the new hosts.
