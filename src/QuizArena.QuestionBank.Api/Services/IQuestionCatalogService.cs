using QuizArena.QuestionBank.Api.Contracts;
using QuizArena.QuestionBank.Api.Security;

namespace QuizArena.QuestionBank.Api.Services;

public interface IQuestionCatalogService
{
    Task<IReadOnlyList<QuestionResponse>> GenerateAsync(AuthenticatedActor actor, int count, CancellationToken cancellationToken);
    Task<IReadOnlyList<QuestionSetResponse>> CreateSnapshotAsync(AuthenticatedActor machine, QuestionSetRequest request, CancellationToken cancellationToken);
}
