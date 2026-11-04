using System.Net;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.QuestionBank.Api.Contracts;
using QuizArena.QuestionBank.Api.Controllers;
using Xunit;

namespace QuizArena.QuestionBank.IntegrationTests.Endpoints;

public sealed class QuestionContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Route_Metadata_MatchesCommittedContract(bool machine)
    {
        // Arrange
        using var contract = ReadContract();
        var path = machine ? "/internal/question-sets" : "/api/questions/generate";
        var verb = machine ? "post" : "get";
        var controller = machine ? typeof(InternalQuestionSetsController) : typeof(QuestionsController);
        var method = controller.GetMethod(machine ? "Create" : "Generate")!;
        var operation = contract.RootElement.GetProperty("paths").GetProperty(path).GetProperty(verb);

        // Act
        var route = method.GetCustomAttribute<HttpMethodAttribute>()!;
        var metadata = method.GetCustomAttributes<ProducesResponseTypeAttribute>().ToArray();

        // Assert
        route.HttpMethods.Should().ContainSingle().Which.Should().Be(verb.ToUpperInvariant());
        ("/" + controller.GetCustomAttribute<RouteAttribute>()!.Template + (route.Template is null ? "" : "/" + route.Template)).Should().Be(path);
        method.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(operation.GetProperty("x-policy").GetString());
        operation.GetProperty("x-actor").GetString().Should().Be(machine ? "machine" : "user");
        metadata.Select(value => value.StatusCode.ToString()).Should().BeEquivalentTo(operation.GetProperty("responses").EnumerateObject().Select(value => value.Name));
        metadata.Single(value => value.StatusCode == 200).Type.Should().Be(machine ? typeof(IReadOnlyList<QuestionSetResponse>) : typeof(IReadOnlyList<QuestionResponse>));
        var schema = operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        schema.GetProperty("type").GetString().Should().Be("array");
        schema.GetProperty("items").GetProperty("$ref").GetString().Should().Be(machine ? "#/components/schemas/QuestionSnapshot" : "#/components/schemas/Question");
        if (machine)
        {
            operation.GetProperty("requestBody").GetProperty("required").GetBoolean().Should().BeTrue();
            schema.GetProperty("minItems").GetInt32().Should().Be(5);
            schema.GetProperty("maxItems").GetInt32().Should().Be(5);
        }
        else
        {
            var count = operation.GetProperty("parameters")[0].GetProperty("schema");
            count.GetProperty("minimum").GetInt32().Should().Be(1);
            count.GetProperty("maximum").GetInt32().Should().Be(50);
            count.GetProperty("default").GetInt32().Should().Be((int)method.GetParameters()[0].DefaultValue!);
        }
    }

    [Fact]
    public void ApiExplorer_InternalRouteAndAnswerTypes_AreExcludedFromBrowserDiscovery()
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();
        using var contract = ReadContract();

        // Act
        var descriptions = fixture.Factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items).ToArray();

        // Assert
        descriptions.Should().NotContain(description => description.RelativePath == "internal/question-sets");
        var publicRoute = descriptions.Should().ContainSingle(description => description.RelativePath == "api/questions/generate").Subject;
        publicRoute.SupportedResponseTypes.Single(response => response.StatusCode == 200).Type.Should().Be(typeof(IReadOnlyList<QuestionResponse>));
        typeof(QuestionResponse).GetProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
            .Should().BeEquivalentTo(contract.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("Question").GetProperty("properties").EnumerateObject().Select(property => property.Name));
        var publicOperation = contract.RootElement.GetProperty("paths").GetProperty("/api/questions/generate");
        publicOperation.GetRawText().Should().NotContain("correctAnswer").And.NotContain("QuestionSnapshot");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Health_AnonymousOrIrrelevantInvalidToken_ReturnsOnlyLiveness(bool invalidToken)
    {
        // Arrange
        using var fixture = new QuestionBankApiFixture();

        // Act
        using var response = await QuestionBankApiFixture.SendAsync(fixture.Client, false, invalidToken ? "SENTINEL" : null, "/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateObject().Select(property => property.Name).Should().ContainSingle().Which.Should().Be("status");
        json.RootElement.GetProperty("status").GetString().Should().Be("ok");
    }

    private static JsonDocument ReadContract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln"))) directory = directory.Parent;
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "contracts", "openapi", "questionbank.yaml")));
    }
}
