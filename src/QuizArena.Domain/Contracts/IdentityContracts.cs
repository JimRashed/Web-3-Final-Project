namespace QuizArena.Domain.Contracts;

public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);
public record LoginResponse(Guid UserId, string Username, string Token);
public record UserDto(Guid UserId, string Username);