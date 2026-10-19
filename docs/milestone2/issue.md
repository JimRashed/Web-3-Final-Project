# Issue: Add GET /api/users/me endpoint to return authenticated user's profile

## Summary
Add a new endpoint GET /api/users/me that returns the currently authenticated user's profile (id, username/email, display name, roles/claims). This supports client-side features that need the user's identity without additional calls.

## Motivation
Many clients need a simple, authenticated endpoint to obtain the current user's profile. This avoids duplicating token parsing logic in multiple places and centralizes the contract for profile data.

## Desired behavior
- Endpoint: GET /api/users/me
- Requires authentication (JWT or existing auth scheme)
- Returns 200 OK with JSON body containing: id, username (or email), displayName, roles (array)
- Returns 401 Unauthorized when the request is unauthenticated

Example response:
```
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "123",
  "username": "alice@example.com",
  "displayName": "Alice",
  "roles": ["User"]
}
```

## Acceptance criteria
1. Endpoint implemented in API project under route /api/users/me
2. Endpoint uses existing authentication pipeline and extracts claims from HttpContext.User
3. Unit tests cover:
   - Authenticated request returns 200 and correct JSON shape
   - Unauthenticated request returns 401
4. Minimal changes to existing services; reuse AuthService or user service if available
5. Local copy of issue saved at docs/milestone2/issue.md and the issue created on GitHub (or draft prepared)

## Implementation notes / approach
- Inspect Api project's controllers and AuthService to determine where to add endpoint.
- Prefer adding a UsersController with [Authorize] attribute and an action GetMe that maps claims to DTO.
- Add a lightweight DTO: CurrentUserDto { Id, Username, DisplayName, Roles }
- Add unit tests in the existing test project (or create tests/UsersControllerTests) using mocked HttpContext/User.

## Test cases
1. Authenticated: mock HttpContext.User with claims (sub, email, name, role) -> expect 200 and JSON with those values
2. Unauthenticated: no principal -> expect 401 Unauthorized

## Labels (suggested)
- enhancement
- milestone/m2

## Related files to inspect
- src/QuizArena.Api/Controllers (existing controllers)
- src/QuizArena.Api/Services/AuthService.cs
- src/QuizArena.Api (startup/auth configuration)

## Local artifacts
- docs/milestone2/issue.md (this file)
