using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.QuestionBank.Api.Contracts;
using Xunit;

namespace QuizArena.QuestionBank.UnitTests.Contracts;

public sealed class QuestionContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Serialize_PublicQuestion_ContainsOnlyAnswerSafeFields()
    {
        // Arrange
        var response = new QuestionResponse(Guid.NewGuid(), "Red planet?", "Science", ["Mars", "Venus", "Earth", "Jupiter"]);

        // Act
        var json = JsonSerializer.SerializeToElement(response, WebJson);

        // Assert
        json.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "prompt", "category", "options");
        json.GetProperty("id").GetGuid().Should().Be(response.Id);
        json.GetProperty("options").GetArrayLength().Should().Be(4);
        json.TryGetProperty("correctAnswer", out _).Should().BeFalse();
    }

    [Fact]
    public void Serialize_InternalQuestionSet_PreservesArrayWithAnswerBearingElements()
    {
        // Arrange
        var response = Enumerable.Range(0, 5).Select(_ => new QuestionSetResponse(
            Guid.NewGuid(), "Red planet?", "Science", ["Mars", "Venus", "Earth", "Jupiter"], "Mars")).ToArray();

        // Act
        var json = JsonSerializer.SerializeToElement(response, WebJson);

        // Assert
        json.ValueKind.Should().Be(JsonValueKind.Array);
        json.GetArrayLength().Should().Be(5);
        json[0].EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "prompt", "category", "options", "correctAnswer");
        json[0].GetProperty("correctAnswer").GetString().Should().Be("Mars");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"count\":null}")]
    [InlineData("{\"count\":0}")]
    [InlineData("{\"count\":4}")]
    [InlineData("{\"count\":6}")]
    public void MvcValidate_QuestionSetCountMissingOrNotFive_AddsCountError(string json)
    {
        // Arrange
        var request = JsonSerializer.Deserialize<QuestionSetRequest>(json, WebJson)!;

        // Act
        var modelState = ValidateWithMvc(request);

        // Assert
        modelState.IsValid.Should().BeFalse();
        modelState[nameof(QuestionSetRequest.Count)]!.Errors.Should().ContainSingle();
    }

    [Fact]
    public void MvcValidate_QuestionSetCountFive_AcceptsRequest()
    {
        // Arrange
        var request = JsonSerializer.Deserialize<QuestionSetRequest>("{\"count\":5}", WebJson)!;

        // Act
        var modelState = ValidateWithMvc(request);

        // Assert
        modelState.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("{\"count\":5,\"userId\":\"another-user\"}")]
    [InlineData("{\"count\":5,\"correctAnswer\":\"Mars\"}")]
    public void Deserialize_QuestionSetUnknownProperty_RejectsRequest(string json)
    {
        // Arrange
        Action act = () => JsonSerializer.Deserialize<QuestionSetRequest>(json, WebJson);

        // Act / Assert
        act.Should().Throw<JsonException>();
    }

    private static ModelStateDictionary ValidateWithMvc(QuestionSetRequest request)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var modelState = new ModelStateDictionary();
        var context = new ActionContext(
            new DefaultHttpContext { RequestServices = provider },
            new RouteData(),
            new ActionDescriptor(),
            modelState);

        provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, string.Empty, request);
        return modelState;
    }
}
