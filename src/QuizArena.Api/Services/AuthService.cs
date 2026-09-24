using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Identity.Entities;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IConfiguration _config;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthService(IUserRepository users, IConfiguration config)
    {
        _users = users;
        _config = config;
    }

    public async Task<UserDto?> RegisterAsync(RegisterRequest request)
    {
        if (await _users.GetByUsernameAsync(request.Username) != null)
            return null;

        var user = new User { Id = Guid.NewGuid(), Username = request.Username };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        await _users.AddAsync(user);
        return new UserDto(user.Id, user.Username);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _users.GetByUsernameAsync(request.Username);
        if (user == null)
            return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        return new LoginResponse(user.Id, user.Username, CreateToken(user));
    }

    public async Task<UserDto?> GetUserAsync(Guid id)
    {
        var user = await _users.GetByIdAsync(id);
        return user == null ? null : new UserDto(user.Id, user.Username);
    }

    // Builds a standard JWT (header.payload.signature) signed with HMAC-SHA256.
    private string CreateToken(User user)
    {
        var header = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" });
        var payload = JsonSerializer.Serialize(new
        {
            sub = user.Id.ToString(),
            name = user.Username,
            iss = "QuizArena",
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        });

        var unsignedToken = $"{Base64Url(Encoding.UTF8.GetBytes(header))}.{Base64Url(Encoding.UTF8.GetBytes(payload))}";

        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);
        var signature = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(unsignedToken));

        return $"{unsignedToken}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}