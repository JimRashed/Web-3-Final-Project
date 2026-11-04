using QuizArena.QuestionBank.Api.Contracts;
using QuizArena.QuestionBank.Api.Security;
using QuizArena.QuestionBank.Domain.Repositories;
using QuizArena.QuestionBank.Domain.Services;

namespace QuizArena.QuestionBank.Api.Services;

public sealed class QuestionCatalogService(IQuestionRepository questions) : IQuestionCatalogService
{
    public async Task<IReadOnlyList<QuestionResponse>> GenerateAsync(AuthenticatedActor actor, int count, CancellationToken cancellationToken)
    {
        EnsureKind(actor, ActorKind.User);
        QuestionSelection.ValidateCount(count);
        cancellationToken.ThrowIfCancellationRequested();

        var selected = await questions.GenerateAsync(count, cancellationToken);
        return selected.Select(question => new QuestionResponse(
            question.Id, question.Prompt, question.Category, question.Options.ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<QuestionSetResponse>> CreateSnapshotAsync(
        AuthenticatedActor machine,
        QuestionSetRequest request,
        CancellationToken cancellationToken)
    {
        EnsureKind(machine, ActorKind.Machine);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Count != 5)
        {
            throw new ArgumentException("Question-set count must be exactly five.", nameof(request));
        }
        cancellationToken.ThrowIfCancellationRequested();

        var snapshots = await questions.GetExactSnapshotsAsync(request.Count.Value, cancellationToken);
        return snapshots.Select(snapshot => new QuestionSetResponse(
            snapshot.Id, snapshot.Prompt, snapshot.Category, snapshot.Options.ToArray(), snapshot.CorrectAnswer)).ToArray();
    }

    private static void EnsureKind(AuthenticatedActor actor, ActorKind kind)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != kind)
        {
            throw new InvalidOperationException($"This operation requires a {kind.ToString().ToLowerInvariant()} actor.");
        }
    }
}
