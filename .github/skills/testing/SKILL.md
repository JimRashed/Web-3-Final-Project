---
name: testing-guidelines
description: Authoritative standards, conventions, and execution patterns for writing and verifying unit and integration tests using xUnit, FluentAssertions, NSubstitute/Moq, and WebApplicationFactory.
---

# Testing Guidelines & Test Automation Standards

Apply these rules whenever authoring, updating, or reviewing unit and integration tests in the repository.

---

## 1. Tooling & Dependencies

* **Runner & Framework:** xUnit (`[Fact]`, `[Theory]`, `[InlineData]`, `[MemberData]`).
* **Assertions:** FluentAssertions (`result.Should().Be(...)`, `action.Should().ThrowAsync<T>()`).
* **Mocking:** NSubstitute (preferred) or Moq.
* **Integration Host:** `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>`.
* **Database Isolation:** EF Core In-Memory provider with clean seeding per fixture.

---

## 2. Test Architecture: Unit vs. Integration

### Unit Tests
* **Target:** Individual domain entities, service implementations, utility classes, and business validators.
* **Isolation Rule:** Zero external I/O. No file system access, network calls, or actual database context instances.
* **Dependencies:** Every external interface (`IRepository`, `ILogger`, `IHttpClientFactory`) must be substituted/mocked.
* **Performance:** Each unit test must execute in under 20 milliseconds.

### Integration Tests
* **Target:** End-to-end HTTP pipeline, controller endpoints, routing, model binding, authorization, middleware, and database persistence.
* **Isolation Rule:** Use `WebApplicationFactory<Program>` configured with an isolated test database (e.g., SQLite in-memory with an open keep-alive connection).
* **State Cleanliness:** Never share mutable database state between test runs. Reset or migrate/seed per test class or use unique database names per run.

---

## 3. The Arrange-Act-Assert (AAA) Standard

Every test method must adhere to the three-phase **Arrange-Act-Assert** pattern. Delineate each phase with clear comments.

### Detailed Phase Breakdown

1. **Arrange (Setup):**
   * Instantiate system-under-test (SUT).
   * Configure mocks/substitutes with specific parameters (avoid loose matchers like `Arg.Any<T>()` unless the value is strictly irrelevant to the assertion).
   * Prepare expected inputs, DTOs, and cancellation tokens.
   * Keep setup minimal: do not configure mocks or variables that the test does not exercise.

2. **Act (Execution):**
   * Execute exactly **one** logical action or method invocation on the SUT.
   * If invoking an asynchronous method, always `await` the call directly.
   * For exception testing, capture the delegate without awaiting it immediately:
     ```csharp
     Func<Task> act = () => sut.ExecuteAsync(invalidInput);
     ```

3. **Assert (Verification):**
   * Verify the direct outcome (return value, status code, exception type).
   * Verify side effects on dependencies only when necessary (e.g., `await repository.Received(1).SaveAsync(...)`).
   * Avoid multiple unrelated assertions in a single test; every assertion must evaluate the outcome of the single action in the Act phase.

---

## 4. Code Structure & Reference Examples

### A. Unit Test Pattern (Service + NSubstitute + FluentAssertions)

```csharp
using FluentAssertions;
using NSubstitute;
using Xunit;

public class UserServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_userRepository);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = new User { Id = userId, Email = "dev@example.com" };
        _userRepository.FindByIdAsync(userId, Arg.Any<CancellationToken>())
                       .Returns(existingUser);

        // Act
        var result = await _sut.GetByIdAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Email.Should().Be("dev@example.com");
    }

    [Fact]
    public async Task CreateAsync_WhenEmailAlreadyExists_ThrowsDuplicateException()
    {
        // Arrange
        var email = "taken@example.com";
        _userRepository.EmailExistsAsync(email).Returns(true);
        var request = new CreateUserRequest(email, "StrongPassword123!");

        // Act
        Func<Task> act = () => _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DuplicateEntityException>()
                 .WithMessage("*already in use*");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}