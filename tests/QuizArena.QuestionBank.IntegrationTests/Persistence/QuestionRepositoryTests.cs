using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.QuestionBank.Data.Context;
using QuizArena.QuestionBank.Data.Repositories;
using QuizArena.QuestionBank.Domain.Entities;
using QuizArena.QuestionBank.Domain.Exceptions;
using Xunit;

namespace QuizArena.QuestionBank.IntegrationTests.Persistence;

public sealed class QuestionRepositoryTests : IAsyncLifetime
{
    private readonly DbContextOptions<QuestionBankDbContext> _options;

    public QuestionRepositoryTests()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True" : server)
        {
            InitialCatalog = $"QuizArenaQuestionBank_Repository_{Guid.NewGuid():N}"
        };
        _options = new DbContextOptionsBuilder<QuestionBankDbContext>().UseSqlServer(builder.ConnectionString).Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new QuestionBankDbContext(_options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = new QuestionBankDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task GetAllAsync_SeededCatalog_ReturnsFiveValidUntrackedQuestions()
    {
        // Arrange
        await using var context = new QuestionBankDbContext(_options);
        var repository = new QuestionRepository(context);
        // Act
        var questions = await repository.GetAllAsync();
        // Assert
        questions.Should().HaveCount(5);
        questions.Should().OnlyContain(question => question.Options.Count == 4 && question.Options.Contains(question.CorrectAnswer));
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_NewQuestion_PersistsOrderedOptionsAcrossContexts()
    {
        // Arrange
        var question = new Question(Guid.NewGuid(), "Prompt", "New category", ["b", "B", "B ", "D"], "B");
        await using (var write = new QuestionBankDbContext(_options))
        {
            // Act
            await new QuestionRepository(write).AddAsync(question);
        }
        // Assert
        await using var read = new QuestionBankDbContext(_options);
        var stored = (await new QuestionRepository(read).GetAllAsync()).Single(item => item.Id == question.Id);
        stored.Options.Should().Equal(question.Options);
        stored.Category.Should().Be(question.Category);
        stored.CorrectAnswer.Should().Be("B");
    }

    [Fact]
    public async Task GenerateAsync_RequestAboveCatalog_ReturnsDistinctAvailableQuestions()
    {
        // Arrange
        await using var context = new QuestionBankDbContext(_options);
        var repository = new QuestionRepository(context);
        // Act
        var questions = await repository.GenerateAsync(50);
        // Assert
        questions.Should().HaveCount(5);
        questions.Select(question => question.Id).Should().OnlyHaveUniqueItems();
        Func<Task> exact = () => repository.GetExactSnapshotsAsync(6);
        await exact.Should().ThrowAsync<InsufficientQuestionCatalogException>();
    }

    [Fact]
    public async Task DeleteCategory_WithQuestions_IsRestrictedByDatabase()
    {
        // Arrange
        await using var context = new QuestionBankDbContext(_options);
        context.Categories.Remove(await context.Categories.SingleAsync());
        // Act
        Func<Task> save = () => context.SaveChangesAsync();
        // Assert
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task AddAsync_DuplicateIdentity_IsRejectedAcrossContexts()
    {
        // Arrange
        await using var read = new QuestionBankDbContext(_options);
        var question = (await new QuestionRepository(read).GetAllAsync())[0];
        await using var write = new QuestionBankDbContext(_options);
        // Act
        Func<Task> add = () => new QuestionRepository(write).AddAsync(question);
        // Assert
        await add.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task GetAllAsync_CancelledToken_ThrowsCancellation()
    {
        // Arrange
        await using var context = new QuestionBankDbContext(_options);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        // Act
        Func<Task> read = () => new QuestionRepository(context).GetAllAsync(cancellation.Token);
        // Assert
        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task Selection_InvalidCount_RejectsBothOperations(int count)
    {
        // Arrange
        await using var context = new QuestionBankDbContext(_options);
        var repository = new QuestionRepository(context);
        // Act
        Func<Task> generate = () => repository.GenerateAsync(count);
        Func<Task> exact = () => repository.GetExactSnapshotsAsync(count);
        // Assert
        await generate.Should().ThrowAsync<InvalidQuestionCountException>();
        await exact.Should().ThrowAsync<InvalidQuestionCountException>();
    }

    [Fact]
    public async Task GetExactSnapshotsAsync_CatalogChanges_PreservesSelectedValues()
    {
        // Arrange
        await using var context = new QuestionBankDbContext(_options);
        var snapshots = await new QuestionRepository(context).GetExactSnapshotsAsync(5);
        var snapshot = snapshots[0];
        var originalPrompt = snapshot.Prompt;
        // Act
        await context.Questions.Where(question => question.Id == snapshot.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(question => question.Prompt, "Updated catalog prompt"));
        // Assert
        snapshot.Prompt.Should().Be(originalPrompt);
        snapshots.Select(question => question.Id).Should().OnlyHaveUniqueItems();
        (await new QuestionRepository(context).GetAllAsync()).Single(question => question.Id == snapshot.Id)
            .Prompt.Should().Be("Updated catalog prompt");
    }

    [Fact]
    public async Task AddAsync_CompetingDuplicateIds_OnlyOneInsertSucceeds()
    {
        // Arrange
        await using var first = new QuestionBankDbContext(_options);
        await using var second = new QuestionBankDbContext(_options);
        var category = await first.Categories.Select(item => item.Name).SingleAsync();
        var question = new Question(Guid.NewGuid(), "Concurrent question", category, ["A", "B", "C", "D"], "A");
        async Task<bool> Insert(QuestionBankDbContext context)
        {
            try
            {
                await new QuestionRepository(context).AddAsync(question);
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }
        // Act
        var results = await Task.WhenAll(Insert(first), Insert(second));
        // Assert
        results.Should().ContainSingle(success => success);
        await using var verify = new QuestionBankDbContext(_options);
        (await verify.Questions.CountAsync(item => item.Id == question.Id)).Should().Be(1);
    }
}
