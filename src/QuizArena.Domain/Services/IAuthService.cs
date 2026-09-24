using QuizArena.Domain.Contracts;

namespace QuizArena.Domain.Services;

public interface IAuthService
{
    Task<UserDto?> RegisterAsync(RegisterRequest request);   // null = username taken
    Task<LoginResponse?> LoginAsync(LoginRequest request);    // null = wrong credentials
    Task<UserDto?> GetUserAsync(Guid id);
}