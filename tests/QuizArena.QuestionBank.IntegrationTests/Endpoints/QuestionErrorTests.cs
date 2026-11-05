using System.Net;
using QuizArena.QuestionBank.Domain.Exceptions;
using Xunit;

namespace QuizArena.QuestionBank.IntegrationTests.Endpoints;

public sealed class QuestionErrorTests
{
    public static IEnumerable<object[]> Failures()
    {
        foreach (var machine in new[] { false, true })
        {
            yield return [machine, new InvalidQuestionCountException(0), HttpStatusCode.BadRequest];
            yield return [machine, new TimeoutException("SENTINEL SQL password and answer"), HttpStatusCode.ServiceUnavailable];
            yield return [machine, new InvalidOperationException("SENTINEL wrapper", new TimeoutException("SENTINEL SQL")), HttpStatusCode.ServiceUnavailable];
            yield return [machine, new InvalidOperationException("SENTINEL unexpected answer"), HttpStatusCode.InternalServerError];
            yield return [machine, new InvalidQuestionException("SENTINEL invalid stored question"), HttpStatusCode.InternalServerError];
        }
        yield return [true, new InsufficientQuestionCatalogException(5, 4), HttpStatusCode.Conflict];
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Route_ApplicationFailure_ReturnsSafeTypedProblem(bool machine, Exception exception, HttpStatusCode status)
    {
        // Arrange
        var service = new CatalogServiceDouble { OnCall = _ => Task.FromException(exception) };
        using var fixture = new QuestionBankApiFixture(service);

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, machine, QuestionBankApiFixture.Token(machine));

        // Assert
        await QuestionBankApiFixture.AssertProblemAsync(response, status);
    }
}
