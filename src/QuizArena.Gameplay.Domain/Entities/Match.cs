using QuizArena.Gameplay.Domain.Enums;
using QuizArena.Gameplay.Domain.Exceptions;

namespace QuizArena.Gameplay.Domain.Entities;

/// <summary>
/// A persisted two-player match: five rounds, one answer per player per round,
/// one point per correct answer. Advancement happens only when both players answered.
/// </summary>
public sealed class Match
{
    public const int RequiredQuestionCount = 5;

    private readonly List<RoundAnswer> _answers = [];

    private Match(
        string matchId,
        string lobbyId,
        string playerAId,
        string playerBId,
        IReadOnlyList<QuestionSnapshot> questions,
        DateTime createdAt)
    {
        MatchId = matchId;
        LobbyId = lobbyId;
        PlayerAId = playerAId;
        PlayerBId = playerBId;
        Questions = questions;
        CreatedAt = createdAt;
    }

    public string MatchId { get; }

    public string LobbyId { get; }

    public string PlayerAId { get; }

    public string PlayerBId { get; }

    public MatchStatus Status { get; private set; } = MatchStatus.Active;

    public int PlayerAScore { get; private set; }

    public int PlayerBScore { get; private set; }

    public int CurrentQuestionIndex { get; private set; }

    public string? WinnerUserId { get; private set; }

    public bool Draw { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyList<QuestionSnapshot> Questions { get; }

    public IReadOnlyList<RoundAnswer> Answers => _answers;

    /// <summary>Current question while the match is active; reading never changes state.</summary>
    public QuestionSnapshot? CurrentQuestion =>
        Status == MatchStatus.Active ? Questions[CurrentQuestionIndex] : null;

    public static Match Create(
        string matchId,
        string lobbyId,
        string playerAId,
        string playerBId,
        IReadOnlyList<QuestionSnapshot> questions,
        DateTime createdAt)
    {
        var validMatchId = RequireIdentity(matchId, "match id", 100);
        var validLobbyId = RequireIdentity(lobbyId, "lobby id", 100);
        var validPlayerA = RequireIdentity(playerAId, "player A", 200);
        var validPlayerB = RequireIdentity(playerBId, "player B", 200);

        if (string.Equals(validPlayerA, validPlayerB, StringComparison.Ordinal))
        {
            throw new InvalidMatchException("A match needs two distinct players.");
        }

        if (questions is null || questions.Count != RequiredQuestionCount)
        {
            throw new InvalidMatchException(
                $"A match needs exactly {RequiredQuestionCount} question snapshots.");
        }

        if (questions.Select(question => question.QuestionId).Distinct().Count() != RequiredQuestionCount)
        {
            throw new InvalidMatchException("Question snapshots must reference distinct questions.");
        }

        EnsureUtc(createdAt);

        return new Match(validMatchId, validLobbyId, validPlayerA, validPlayerB, [.. questions], createdAt);
    }

    public bool IsMember(string userId) =>
        string.Equals(userId, PlayerAId, StringComparison.Ordinal) ||
        string.Equals(userId, PlayerBId, StringComparison.Ordinal);

    /// <summary>
    /// Records a player's answer to the current round and advances the match when both players answered.
    /// </summary>
    public AnswerOutcome SubmitAnswer(string userId, Guid questionId, string answer, DateTime answeredAt)
    {
        if (!IsMember(userId))
        {
            throw new MatchAccessDeniedException(MatchId, userId);
        }

        if (Status == MatchStatus.Finished)
        {
            throw new MatchConflictException(MatchConflictReason.MatchFinished, "The match is already finished.");
        }

        var question = Questions[CurrentQuestionIndex];
        if (question.QuestionId != questionId)
        {
            throw new MatchConflictException(
                MatchConflictReason.StaleQuestion,
                "The submitted question is not the current round's question.");
        }

        if (HasAnswered(userId, CurrentQuestionIndex))
        {
            throw new MatchConflictException(
                MatchConflictReason.DuplicateAnswer,
                "The player already answered this round.");
        }

        if (string.IsNullOrWhiteSpace(answer) || !question.Options.Contains(answer, StringComparer.Ordinal))
        {
            throw new InvalidMatchException("The answer must be one of the offered options.");
        }

        EnsureUtc(answeredAt);

        var correct = question.IsCorrect(answer);
        if (correct)
        {
            if (string.Equals(userId, PlayerAId, StringComparison.Ordinal))
            {
                PlayerAScore++;
            }
            else
            {
                PlayerBScore++;
            }
        }

        _answers.Add(new RoundAnswer(
            MatchId,
            CurrentQuestionIndex,
            userId,
            questionId,
            answer,
            correct,
            answeredAt));

        var roundComplete =
            HasAnswered(PlayerAId, CurrentQuestionIndex) &&
            HasAnswered(PlayerBId, CurrentQuestionIndex);

        if (roundComplete)
        {
            Advance(answeredAt);
        }

        return new AnswerOutcome(
            correct,
            PlayerAScore,
            PlayerBScore,
            roundComplete,
            Status == MatchStatus.Finished);
    }

    private bool HasAnswered(string userId, int roundIndex) =>
        _answers.Any(answer =>
            answer.RoundIndex == roundIndex &&
            string.Equals(answer.UserId, userId, StringComparison.Ordinal));

    private void Advance(DateTime at)
    {
        if (CurrentQuestionIndex == Questions.Count - 1)
        {
            Finish(at);
            return;
        }

        CurrentQuestionIndex++;
    }

    private void Finish(DateTime at)
    {
        Status = MatchStatus.Finished;
        CompletedAt = at;

        if (PlayerAScore == PlayerBScore)
        {
            Draw = true;
            WinnerUserId = null;
            return;
        }

        Draw = false;
        WinnerUserId = PlayerAScore > PlayerBScore ? PlayerAId : PlayerBId;
    }

    private static string RequireIdentity(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidMatchException($"The {name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidMatchException($"The {name} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidMatchException("Timestamps must be expressed in UTC.");
        }
    }
}
