using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using QuizArena.Api.Services;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Identity.Entities;

namespace QuizArena.UnitTests.Services;

public sealed class AuthServiceTests
{
    private static AuthService Create(Mock<IUserRepository> users) =>
        new(users.Object, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "test-key-that-is-long-enough" }).Build());

    [Fact]
    public async Task RegisterAsync_creates_user_and_returns_dto()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByUsernameAsync("alice")).ReturnsAsync((User?)null);
        var service = Create(users);

        var result = await service.RegisterAsync(new RegisterRequest("alice", "password"));

        result.Should().NotBeNull();
        result!.Username.Should().Be("alice");
        users.Verify(x => x.AddAsync(It.Is<User>(u => u.Username == "alice" && u.PasswordHash != "password")), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_returns_null_for_duplicate_username()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByUsernameAsync("alice")).ReturnsAsync(new User { Username = "alice" });

        var result = await Create(users).RegisterAsync(new RegisterRequest("alice", "password"));

        result.Should().BeNull();
        users.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_returns_token_for_valid_password_and_null_for_invalid_password()
    {
        User? savedUser = null;
        var registerUsers = new Mock<IUserRepository>();
        registerUsers.Setup(x => x.GetByUsernameAsync("alice")).ReturnsAsync(() => savedUser);
        registerUsers.Setup(x => x.AddAsync(It.IsAny<User>())).Callback<User>(user => savedUser = user).Returns(Task.CompletedTask);
        var service = Create(registerUsers);

        await service.RegisterAsync(new RegisterRequest("alice", "password"));

        (await service.LoginAsync(new LoginRequest("alice", "password"))).Should().NotBeNull();
        (await service.LoginAsync(new LoginRequest("alice", "wrong"))).Should().BeNull();
        (await service.LoginAsync(new LoginRequest("missing", "password"))).Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_maps_existing_user_and_returns_null_when_missing()
    {
        var id = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(new User { Id = id, Username = "alice" });
        users.Setup(x => x.GetByIdAsync(It.Is<Guid>(value => value != id))).ReturnsAsync((User?)null);

        (await Create(users).GetUserAsync(id)).Should().Be(new UserDto(id, "alice"));
        (await Create(users).GetUserAsync(Guid.NewGuid())).Should().BeNull();
    }
}
