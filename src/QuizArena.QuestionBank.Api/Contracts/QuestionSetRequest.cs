using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QuizArena.QuestionBank.Api.Contracts;

/// <summary>Machine-only snapshot request; count must be supplied as exactly five.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class QuestionSetRequest
{
    public QuestionSetRequest()
    {
    }

    public QuestionSetRequest(int? count)
    {
        Count = count;
    }

    [Required, Range(5, 5)]
    public int? Count { get; init; }
}
