# Testing Rules for AI Agent

When implementing new features or modifying existing code, you MUST follow these testing rules:

1. **Test Coverage**: Every new Controller endpoint or Service method must have corresponding unit tests.
2. **Test Location**: All unit tests must be placed in the `tests/QuizArena.UnitTests/` directory.
3. **Test Naming**: Test methods must follow the pattern `MethodName_Scenario_ExpectedBehavior`.
4. **Assertions**: Use xUnit and FluentAssertions. Every test must have at least one explicit assertion.
5. **Mocking**: Use Moq for mocking dependencies in Service tests. Do not call the real database in unit tests.
6. **Integration Tests**: If adding a new route, add a corresponding integration test in `tests/QuizArena.IntegrationTests/Endpoints/`.