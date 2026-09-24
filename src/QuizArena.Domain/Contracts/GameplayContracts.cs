namespace QuizArena.Domain.Contracts;

public record CreateMatchRequest(string MatchId, string PlayerAId, string PlayerBId);

public record MatchResponse(
    string MatchId,
    string PlayerAId,
    string PlayerBId,
    string Status,
    int PlayerAScore,
    int PlayerBScore,
    int CurrentQuestionIndex);

public record QuestionResponse(string QuestionId, string Text, List<string> Options);

public record SubmitAnswerRequest(string UserId, string QuestionId, string Answer);

public record SubmitAnswerResponse(bool Correct, int PlayerAScore, int PlayerBScore);