using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Gameplay.Entities;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Services;

public class GameplayService : IGameplayService
{
    private readonly IMatchRepository _repo;
    private readonly IQuestionService _questions;
    private readonly INotificationService _notify;

    public GameplayService(
        IMatchRepository repo,
        IQuestionService questions,
        INotificationService notify)
    {
        _repo = repo;
        _questions = questions;
        _notify = notify;
    }

    public async Task<MatchResponse> CreateMatchAsync(CreateMatchRequest req)
    {
        var match = new ActiveMatch
        {
            MatchId = req.MatchId,
            PlayerAId = req.PlayerAId,
            PlayerBId = req.PlayerBId,
            Status = "ACTIVE",
            CurrentQuestionIndex = 0
        };

        var saved = await _repo.CreateAsync(match);

        await _notify.LogEventAsync(new CreateEventRequest(
            "MATCH_CREATED", saved.PlayerAId,
            $"{{\"matchId\":\"{saved.MatchId}\"}}"));

        await _notify.LogEventAsync(new CreateEventRequest(
            "MATCH_CREATED", saved.PlayerBId,
            $"{{\"matchId\":\"{saved.MatchId}\"}}"));

        return ToResponse(saved);
    }

    public async Task<MatchResponse?> GetMatchAsync(string matchId)
    {
        var match = await _repo.GetAsync(matchId);
        return match == null ? null : ToResponse(match);
    }

    public async Task<QuestionResponse> GetNextQuestionAsync(string matchId)
    {
        var match = await _repo.GetAsync(matchId);
        if (match == null) throw new Exception("Match not found");

        var q = await _questions.GetNextQuestionAsync(match.CurrentQuestionIndex);
        return q;
    }

    public async Task<SubmitAnswerResponse> SubmitAnswerAsync(string matchId, SubmitAnswerRequest req)
    {
        var match = await _repo.GetAsync(matchId);
        if (match == null) throw new Exception("Match not found");

        var correct = await _questions.ValidateAnswerAsync(req.QuestionId, req.Answer);

        if (correct)
        {
            if (req.UserId == match.PlayerAId) match.PlayerAScore++;
            else if (req.UserId == match.PlayerBId) match.PlayerBScore++;
        }

        match.CurrentQuestionIndex++;

        if (match.CurrentQuestionIndex >= 5)
        {
            match.Status = "FINISHED";
            var winner = match.PlayerAScore >= match.PlayerBScore
                ? match.PlayerAId
                : match.PlayerBId;

            await _notify.LogEventAsync(new CreateEventRequest(
                "MATCH_COMPLETED", match.PlayerAId,
                $"{{\"matchId\":\"{matchId}\",\"winner\":\"{winner}\"}}"));

            await _notify.LogEventAsync(new CreateEventRequest(
                "MATCH_COMPLETED", match.PlayerBId,
                $"{{\"matchId\":\"{matchId}\",\"winner\":\"{winner}\"}}"));
        }

        await _repo.UpdateAsync(match);

        return new SubmitAnswerResponse(correct, match.PlayerAScore, match.PlayerBScore);
    }

    private static MatchResponse ToResponse(ActiveMatch m) =>
        new(m.MatchId, m.PlayerAId, m.PlayerBId, m.Status,
            m.PlayerAScore, m.PlayerBScore, m.CurrentQuestionIndex);
}