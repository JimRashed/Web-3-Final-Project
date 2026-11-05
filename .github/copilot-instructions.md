# Copilot Instructions

## Repository Instructions

### Overview
- **Language & Runtime:** C# targeting .NET 10 with nullable reference types enabled (`<Nullable>enable</Nullable>`) and treat warnings as errors where applicable.
- **Frameworks:** ASP.NET Core Web API, Entity Framework Core, and modern testing frameworks (xUnit/NUnit/MSTest).
- **Architecture:** Service-oriented architecture adhering to clean separation of concerns: API/Host -> Application/Domain -> Infrastructure/Persistence.

### Project layout
- `src/`
  - `.Api/`: Web API entry point, controllers/minimal API endpoints, filters, middleware, and dependency registration.
  - `.Domain/`: Domain entities, business interfaces, validation logic, domain exceptions, and service implementations.
  - `.Data/`: EF Core `DbContext`, entity type configurations, migrations, external service adapters, and repository implementations.
- `tests/`
  - `.UnitTests/`: Fast, isolated tests for domain entities, business logic, and service layers (mocking external dependencies).
  - `.IntegrationTests/`: End-to-end endpoint tests (`WebApplicationFactory`) and database persistence tests (e.g., Testcontainers or in-memory SQLite).
- `docs/`: Setup instructions, architectural decision records (ADRs), and database schema diagrams.
- `.github/workflows/`: CI pipeline configurations for build, test, and lint validation.

### Development rules
- **Endpoint Responsibility:** Keep API controllers or minimal endpoints thin. They should handle HTTP mapping, request validation, calling application services, and returning standard HTTP status codes.
- **Business Logic:** Place domain validation and business rules strictly inside application services or domain models, never inside controllers, EF Core configurations, or database triggers.
- **Dependency Injection:** Use constructor-based dependency injection exclusively. Register services with appropriate lifetimes (`Scoped` for DbContext and business services, `Transient` for lightweight stateless helpers, `Singleton` for caches or configuration).
- **Asynchronous Code:** Prefer `async`/`await` end-to-end down to the database calls (e.g., `ToListAsync()`, `FirstOrDefaultAsync()`). Never use `.Result` or `.Wait()`. Accept and propagate `CancellationToken` in I/O operations.
- **Data Access & DTOs:**
  - Never expose EF Core entities directly to API responses or inputs; map through dedicated Request/Response DTOs or Records.
  - Use `.AsNoTracking()` for read-only database queries.
  - Apply EF Core configurations via `IEntityTypeConfiguration<T>` in separate classes instead of bloating `OnModelCreating`.
- **Naming Conventions:**
  - `PascalCase`: Classes, interfaces, records, methods, public properties, and constants.
  - `camelCase`: Method parameters and local variables.
  - `_camelCase`: Private instance fields.
  - `I` prefix for interfaces (`IQuizService`, `IUserRepository`).
  - `Async` suffix for asynchronous methods returning `Task` or `ValueTask`.

### Build and tests
- Run commands from the repository root:
  1. **Restore:** `dotnet restore`
  2. **Build:** `dotnet build --no-restore`
  3. **Format Check:** `dotnet format --verify-no-changes`
  4. **Run Tests:** `dotnet test --no-build --verbosity normal`
- **Testing Standard:** Add or update unit tests for any changed business rules. Integration tests should accompany new or modified HTTP endpoints. Ensure tests are deterministic and independent of execution order.

### Security
- **Secrets Management:** Never commit raw API keys, production tokens, connection strings, or environment-specific credentials. Use `dotnet user-secrets` for local development and environment variables or Azure Key Vault/GitHub Secrets in CI/CD.
- **Access Control:** Do not bypass, disable, or comment out `[Authorize]` attributes, policy handlers, or role requirements to fix failing endpoints or tests.
- **Input & Persistence:**
  - Parameterize all database queries (standard EF Core LINQ avoids SQL injection; avoid raw interpolated strings in `FromSqlRaw`).
  - Sanitize and validate request payloads at the API boundary before passing them to application services.

### Agent Workflow: R-PIV Protocol
Whenever assigned a task or GitHub issue:
1. **Research:** Inspect the issue, existing code, dependencies and constraints; return a verified context brief.
2. **Plan:** Review the research and save a complete file-by-file plan at `specs/issue-<number>-plan.md`, with acceptance coverage and validation.
3. **Implement and Test:** Execute the plan in small increments with meaningful tests in existing projects. Descriptive test names distinguish features; a dispatcher-specific folder is unnecessary.
4. **Validate and Review:** Execute real checks, then read-only code review. Dispatcher may return actionable failures to Implement for at most two additional repair cycles by default.
5. **Automatic Handoffs:** A single Dispatcher invocation runs the complete sequence. No intermediate CONFIRM/APPROVE or separate user call is required. Genuine missing requirements, unsafe actions, unavailable permissions/tools or provider limits still block honestly.
6. **Final Human Gate:** Only Dispatcher commits/pushes issue-specific changes and opens a draft PR after checks/review pass. Include issue link, plan, actual validation, review findings and pending human signoff. An independent human reviews before merge; agents never self-approve or merge.

### Change checklist
- [ ] Code compiles without warnings (`dotnet build`).
- [ ] Code conforms to format guidelines (`dotnet format`).
- [ ] All new and existing unit and integration tests pass (`dotnet test`).
- [ ] No EF Core entities leaked into API contracts (verified DTO usage).
- [ ] Cancellation tokens are accepted and forwarded in async workflows.
- [ ] Git diff inspected to verify no debug code, test credentials, or unwanted files were introduced.

### Dispatcher entry point
Use `.github/agents/dispatcher.agent.md` and `.github/skills/dispatcher/SKILL.md`. Dispatcher is a custom agent coordinating the host's actual subagent tool, not an executable program. Worker profiles use exact names prime, plan, implement, testing, validate and code-review. Stage workers return to Dispatcher without publishing Git/GitHub changes. Preserve unrelated local changes, use isolated issue branches/worktrees, and keep local `.dispatcher/` reports untracked. Dry-run is read-only. For dispatched work, this final-only human review policy supersedes intermediate-gate wording in historical plans/milestone backlog documents; other explicit scope/security constraints continue to apply.
