namespace QuizArena.QuestionBank.Api.Security;

public enum ActorKind
{
    User,
    Machine,
}

public sealed record AuthenticatedActor(string Issuer, string Subject, ActorKind Kind);
