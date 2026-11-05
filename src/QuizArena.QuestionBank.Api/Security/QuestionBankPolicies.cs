namespace QuizArena.QuestionBank.Api.Security;

/// <summary>Named policies from docs/milestone3/authorization-matrix.md; each name equals its scope.</summary>
public static class QuestionBankPolicies
{
    public const string QuestionsRead = "questions.read";
    public const string QuestionsPlay = "questions.play";

    public static IReadOnlyDictionary<string, ActorKind> All { get; } = new Dictionary<string, ActorKind>(StringComparer.Ordinal)
    {
        [QuestionsRead] = ActorKind.User,
        [QuestionsPlay] = ActorKind.Machine,
    };
}
