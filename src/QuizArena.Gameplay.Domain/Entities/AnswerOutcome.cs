namespace QuizArena.Gameplay.Domain.Entities;

/// <summary>Result of submitting one answer.</summary>
public sealed record AnswerOutcome(
    bool Correct,
    int PlayerAScore,
    int PlayerBScore,
    bool RoundComplete,
    bool Finished);
