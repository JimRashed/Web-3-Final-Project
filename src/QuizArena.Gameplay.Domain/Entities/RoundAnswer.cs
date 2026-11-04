namespace QuizArena.Gameplay.Domain.Entities;

/// <summary>One player's answer to one round of a match. Append-only.</summary>
public sealed record RoundAnswer(
    string MatchId,
    int RoundIndex,
    string UserId,
    Guid QuestionId,
    string Answer,
    bool Correct,
    DateTime AnsweredAt);
