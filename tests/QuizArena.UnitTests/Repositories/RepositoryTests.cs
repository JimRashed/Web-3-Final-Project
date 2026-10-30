using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Data.Context;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Identity.Entities;

namespace QuizArena.UnitTests.Repositories;

public sealed class RepositoryTests
{
    [Fact]
    public async Task UserRepository_adds_and_reads_user_with_sql_server_model()
    {
        var options = new DbContextOptionsBuilder<QuizArenaContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new QuizArenaContext(options);
        var repository = new UserRepository(context);
        var user = new User { Id = Guid.NewGuid(), Username = "alice", PasswordHash = "hash" };

        await repository.AddAsync(user);

        (await repository.GetByUsernameAsync("alice")).Should().BeEquivalentTo(user);
    }
}
