using System.Text.Json.Serialization;
using QuizArena.Gameplay.Domain.Entities;

namespace QuizArena.Gameplay.Api.Contracts;

/// <summary>Body of <c>POST /matches</c>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateMatchRequest(string JoinCode);

/// <summary>A match as returned by the API; the score and outcome are the persisted values.</summary>
public sealed record MatchResponse(
    string MatchId,
    string LobbyId,
    string PlayerAId,
    string PlayerBId,
    string Status,
    int PlayerAScore,
    int PlayerBScore,
    int CurrentQuestionIndex,
    string? WinnerUserId,
    bool Draw,
    DateTime? CompletedAt)
{
    public static MatchResponse FromDomain(Match match) =>
        new(
            match.MatchId,
            match.LobbyId,
            match.PlayerAId,
            match.PlayerBId,
            match.Status == Domain.Enums.MatchStatus.Finished ? "FINISHED" : "ACTIVE",
            match.PlayerAScore,
            match.PlayerBScore,
            match.CurrentQuestionIndex,
            match.WinnerUserId,
            match.Draw,
            match.CompletedAt);
}

/// <summary>The current question without its correct answer.</summary>
public sealed record CurrentQuestionResponse(Guid QuestionId, string Text, IReadOnlyList<string> Options)
{
    public static CurrentQuestionResponse FromDomain(QuestionSnapshot snapshot) =>
        new(snapshot.QuestionId, snapshot.Text, snapshot.Options);
}

/// <summary>Body of <c>POST /matches/{matchId}/answers</c>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitAnswerRequest(Guid QuestionId, string Answer);

/// <summary>Result of a submission.</summary>
public sealed record AnswerOutcomeResponse(
    bool Correct,
    int PlayerAScore,
    int PlayerBScore,
    bool RoundComplete,
    bool Finished)
{
    public static AnswerOutcomeResponse FromDomain(AnswerOutcome outcome) =>
        new(outcome.Correct, outcome.PlayerAScore, outcome.PlayerBScore, outcome.RoundComplete, outcome.Finished);
}
