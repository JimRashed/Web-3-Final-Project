# Validation Rules for AI Agent

After writing or modifying any code, you MUST validate your changes by following these steps exactly:

1. **Build**: Run `dotnet build` from the repository root. If there are any compilation errors, read the error output and fix the code. Repeat until the build succeeds with 0 errors.
2. **Test**: Run `dotnet test` from the repository root. If any tests fail, read the failure output, identify the root cause, and fix the code. Repeat until all tests pass (0 failures).
3. **Review**: Before suggesting a commit, ensure no debug statements (like `Console.WriteLine`) are left in the code.