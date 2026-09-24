using QuizArena.Domain.Contracts;

namespace QuizArena.Domain.Services;

public interface IGameplayService
{
    Task<MatchResponse> CreateMatchAsync(CreateMatchRequest req);
    Task<MatchResponse?> GetMatchAsync(string matchId);
    Task<QuestionResponse> GetNextQuestionAsync(string matchId);
    Task<SubmitAnswerResponse> SubmitAnswerAsync(string matchId, SubmitAnswerRequest req);
}