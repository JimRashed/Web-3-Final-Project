using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Gameplay.Api.Errors;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Domain.Exceptions;
using Xunit;

namespace QuizArena.Gameplay.IntegrationTests.Errors;

/// <summary>Every known gameplay failure maps to a ProblemDetails status; nothing expected becomes a 500.</summary>
public sealed class GameplayExceptionHandlerTests
{
    public static TheoryData<Exception, int> MappedExceptions() => new()
    {
        { new GameplayRequestException(StatusCodes.Status404NotFound, "unknown"), StatusCodes.Status404NotFound },
        { new MatchAccessDeniedException("match-1", "player-x"), StatusCodes.Status403Forbidden },
        { new MatchConflictException(MatchConflictReason.DuplicateAnswer, "duplicate"), StatusCodes.Status409Conflict },
        { new MatchConcurrencyException("match-1"), StatusCodes.Status409Conflict },
    };

    [Theory]
    [MemberData(nameof(MappedExceptions))]
    public async Task TryHandleAsync_WithKnownGameplayException_WritesItsStatus(Exception exception, int expected)
    {
        // Arrange
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var sut = new GameplayExceptionHandler(services.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>());

        // Act
        var handled = await sut.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task TryHandleAsync_WithUnknownException_LeavesItToTheGeneric500()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var sut = new GameplayExceptionHandler(services.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>());

        // Act
        var handled = await sut.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_WithOwnershipDenial_WritesCanonicalProblem()
    {
        // Arrange
        using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var sut = new GameplayExceptionHandler(services.GetRequiredService<IProblemDetailsService>());

        // Act
        var handled = await sut.TryHandleAsync(context, new MatchAccessDeniedException("match-1", "player-x"), CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        const string forbidden = "Insufficient scope, actor kind or ownership";
        json.RootElement.GetProperty("title").GetString().Should().Be(forbidden);
        json.RootElement.GetProperty("detail").GetString().Should().Be(forbidden);
    }
}
