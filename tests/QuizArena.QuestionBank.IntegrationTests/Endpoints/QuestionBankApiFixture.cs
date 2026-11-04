using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizArena.QuestionBank.Api.Contracts;
using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.Api.Services;
using QuizArena.QuestionBank.Domain.Entities;
using QuizArena.QuestionBank.Domain.Exceptions;
using QuizArena.QuestionBank.Domain.Repositories;
using QuizArena.QuestionBank.IntegrationTests.Security;

namespace QuizArena.QuestionBank.IntegrationTests.Endpoints;

internal sealed class QuestionBankApiFixture : IDisposable
{
    private readonly SecuredApiFactory _root = new();
    public WebApplicationFactory<Api.Program> Factory { get; }
    public HttpClient Client { get; }
    public CatalogRepository Catalog { get; } = new();

    public QuestionBankApiFixture(IQuestionCatalogService? service = null)
    {
        Factory = _root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IQuestionRepository>();
            services.AddSingleton<IQuestionRepository>(Catalog);
            if (service is not null)
            {
                services.RemoveAll<IQuestionCatalogService>();
                services.AddSingleton(service);
            }
        }));
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public static string Token(bool machine = false, string? scope = null) => TestTokens.Create(
        machine ? ActorKind.Machine : ActorKind.User, scope ?? (machine ? QuestionBankPolicies.QuestionsPlay : QuestionBankPolicies.QuestionsRead));

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, bool machine, string? token, string? path = null, HttpContent? content = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(machine ? HttpMethod.Post : HttpMethod.Get,
            path ?? (machine ? "/internal/question-sets" : "/api/questions/generate"));
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content ?? (machine ? JsonContent.Create(new { count = 5 }) : null);
        request.Headers.Add("X-User-Id", "spoofed-user");
        return await client.SendAsync(request, cancellationToken);
    }

    public static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        var problem = json.RootElement;
        problem.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("type", "title", "status", "detail", "traceId");
        problem.GetProperty("status").GetInt32().Should().Be((int)status);
        Uri.IsWellFormedUriString(problem.GetProperty("type").GetString(), UriKind.Absolute).Should().BeTrue();
        problem.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("traceId").GetString()!.Length.Should().BeInRange(1, 100);
        text.Should().NotContain("SENTINEL").And.NotContain("correctAnswer").And.NotContain("stackTrace");
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        _root.Dispose();
    }
}

internal sealed class CatalogRepository : IQuestionRepository
{
    public Question[] Questions { get; set; } = Enumerable.Range(1, 12).Select(index => new Question(
        Guid.NewGuid(), $"Question {index}?", "Science", ["A", "B", "C", "D"], "A")).ToArray();
    public int? LastCount { get; private set; }

    public Task<IReadOnlyList<Question>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Question>>(Questions);
    public Task AddAsync(Question question, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Question>> GenerateAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCount = count;
        return Task.FromResult<IReadOnlyList<Question>>(Questions.Take(count).ToArray());
    }
    public Task<IReadOnlyList<QuestionSnapshot>> GetExactSnapshotsAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCount = count;
        if (Questions.Length < count) throw new InsufficientQuestionCatalogException(count, Questions.Length);
        return Task.FromResult<IReadOnlyList<QuestionSnapshot>>(Questions.Take(count).Select(question => new QuestionSnapshot(question)).ToArray());
    }
}

internal sealed class CatalogServiceDouble : IQuestionCatalogService
{
    public AuthenticatedActor? Actor { get; private set; }
    public CancellationToken Token { get; private set; }
    public Func<CancellationToken, Task>? OnCall { get; init; }
    public int Calls { get; private set; }

    private async Task RecordAsync(AuthenticatedActor actor, CancellationToken cancellationToken)
    {
        Actor = actor;
        Token = cancellationToken;
        Calls++;
        if (OnCall is not null) await OnCall(cancellationToken);
    }

    public async Task<IReadOnlyList<QuestionResponse>> GenerateAsync(AuthenticatedActor actor, int count, CancellationToken cancellationToken)
    {
        await RecordAsync(actor, cancellationToken);
        return [];
    }
    public async Task<IReadOnlyList<QuestionSetResponse>> CreateSnapshotAsync(AuthenticatedActor machine, QuestionSetRequest request, CancellationToken cancellationToken)
    {
        await RecordAsync(machine, cancellationToken);
        return [];
    }
}
