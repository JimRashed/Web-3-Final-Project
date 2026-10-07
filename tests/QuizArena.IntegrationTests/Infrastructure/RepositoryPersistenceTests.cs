using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuizArena.Data.Context;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Identity.Entities;

namespace QuizArena.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public sealed class RepositoryPersistenceTests(QuizArenaWebApplicationFactory factory)
{
    [Fact]
    public async Task User_repository_persists_and_reads_a_user()
    {
        await using var context = new QuizArenaContext(new DbContextOptionsBuilder<QuizArenaContext>().UseSqlServer(factory.ConnectionString).Options);
        var repository = new UserRepository(context);
        var user = new User { Id = Guid.NewGuid(), Username = $"repo-{Guid.NewGuid():N}", PasswordHash = "hash" };

        await repository.AddAsync(user);

        (await repository.GetByIdAsync(user.Id)).Should().BeEquivalentTo(user);
    }
}
