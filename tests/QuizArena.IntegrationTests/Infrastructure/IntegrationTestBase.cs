namespace QuizArena.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase(QuizArenaWebApplicationFactory factory) : IAsyncLifetime
{
    protected QuizArenaWebApplicationFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
