using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizArena.QuestionBank.Api.Services;
using QuizArena.QuestionBank.Data.Context;
using QuizArena.QuestionBank.Data.Repositories;
using QuizArena.QuestionBank.Domain.Repositories;
using QuizArena.QuestionBank.IntegrationTests.Security;
using Xunit;

namespace QuizArena.QuestionBank.IntegrationTests.Endpoints;

public sealed class QuestionBankSqlApiFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"QuizArenaQuestionBank_Http_{Guid.NewGuid():N}";
    private readonly string _connectionString;
    private readonly DbContextOptions<QuestionBankDbContext> _options;
    private readonly SecuredApiFactory _root = new();
    private WebApplicationFactory<Api.Program>? _factory;
    public HttpClient Client { get; private set; } = null!;

    public QuestionBankSqlApiFixture()
    {
        var server = Environment.GetEnvironmentVariable("QUIZARENA_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server)
            ? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True" : server)
        {
            InitialCatalog = _databaseName
        };
        _connectionString = connection.ConnectionString;
        _options = new DbContextOptionsBuilder<QuestionBankDbContext>().UseSqlServer(_connectionString).Options;
    }

    public async Task InitializeAsync()
    {
        _factory = _root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<QuestionBankDbContext>();
            services.RemoveAll<DbContextOptions<QuestionBankDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<QuestionBankDbContext>>();
            services.AddScoped(_ => new QuestionBankDbContext(_options));
        }));
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        GuardOwnedDatabase(context);
        // Retain and verify production services, rather than silently testing a repository double.
        if (scope.ServiceProvider.GetRequiredService<IQuestionRepository>() is not QuestionRepository
            || scope.ServiceProvider.GetRequiredService<IQuestionCatalogService>() is not QuestionCatalogService)
        {
            throw new InvalidOperationException("SQL endpoint tests require production QuestionBank services.");
        }

        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        await _root.DisposeAsync();
        await using var context = new QuestionBankDbContext(_options);
        GuardOwnedDatabase(context);
        await context.Database.EnsureDeletedAsync();
    }

    public async Task<CatalogState> ReadCatalogAsync()
    {
        await using var context = new QuestionBankDbContext(_options);
        var questions = await context.Questions.AsNoTracking().OrderBy(question => question.Id)
            .Select(question => new StoredQuestion(question.Id, question.Prompt, question.CategoryId, question.CorrectAnswer)).ToArrayAsync();
        var categories = await context.Categories.AsNoTracking().OrderBy(category => category.Id)
            .Select(category => new StoredCategory(category.Id, category.Name)).ToArrayAsync();
        var options = await context.QuestionOptions.AsNoTracking().OrderBy(option => option.QuestionId).ThenBy(option => option.Position)
            .Select(option => new StoredOption(option.QuestionId, option.Position, option.Value)).ToArrayAsync();
        return new CatalogState(questions, categories, options);
    }

    public async Task RemoveOneQuestionAsync()
    {
        await using var context = new QuestionBankDbContext(_options);
        GuardOwnedDatabase(context);
        var question = await context.Questions.OrderBy(item => item.Id).FirstAsync();
        context.Questions.Remove(question);
        await context.SaveChangesAsync();
    }

    private void GuardOwnedDatabase(QuestionBankDbContext context)
    {
        var expected = new SqlConnectionStringBuilder(_connectionString);
        var actual = context.Database.GetDbConnection();
        if (!string.Equals(actual.Database, _databaseName, StringComparison.Ordinal)
            || !string.Equals(actual.DataSource, expected.DataSource, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQL endpoint tests may access only their generated database.");
        }
    }
}

public sealed record CatalogState(StoredQuestion[] Questions, StoredCategory[] Categories, StoredOption[] Options);
public sealed record StoredQuestion(Guid Id, string Prompt, Guid CategoryId, string CorrectAnswer);
public sealed record StoredCategory(Guid Id, string Name);
public sealed record StoredOption(Guid QuestionId, int Position, string Value);
