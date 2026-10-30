using Xunit;

namespace QuizArena.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<QuizArenaWebApplicationFactory>
{
    public const string Name = "QuizArena integration tests";
}
