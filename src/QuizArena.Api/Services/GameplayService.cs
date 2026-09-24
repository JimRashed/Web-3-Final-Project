using QuizArena.Data.Repositories;
using QuizArena.Domain.Contracts;
using QuizArena.Domain.Gameplay.Entities;
using QuizArena.Domain.Services;

namespace QuizArena.Api.Services;

public class GameplayService : IGameplayService
{
    private readonly IMatchRepository _repo;
    private readonly INotificationService _notify;

    // Mock 题目数据，今晚先不依赖 QuestionBank
    private static readonly List<(string Id, string Text, List<string> Options, string Answer)> _mockQuestions = new()
    {
        ("q-1", "What is the chemical formula for water?",
            new List<string>{"H2O","CO2","O2","NaCl"}, "H2O"),
        ("q-2", "What is 2 + 2?", new List<string>{"3","4","5","6"}, "4"),
        ("q-3", "Which planet is known as the Red Planet?",
            new List<string>{"Venus","Mars","Jupiter","Saturn"}, "Mars"),
        ("q-4", "What is the capital of France?",
            new List<string>{"London","Berlin","Paris","Madrid"}, "Paris"),
        ("q-5", "Who wrote Romeo and Juliet?",
            new List<string>{"Dickens","Shakespeare","Tolstoy","Hemingway"}, "Shakespeare"),
    };

    public GameplayService(IMatchRepository repo, INotificationService notify)
    {
        _repo = repo;
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

        var q = _mockQuestions[match.CurrentQuestionIndex % _mockQuestions.Count];
        return new QuestionResponse(q.Id, q.Text, q.Options);
    }

    public async Task<SubmitAnswerResponse> SubmitAnswerAsync(string matchId, SubmitAnswerRequest req)
    {
        var match = await _repo.GetAsync(matchId);
        if (match == null) throw new Exception("Match not found");

        var q = _mockQuestions.FirstOrDefault(x => x.Id == req.QuestionId);
        var correct = q.Answer == req.Answer;

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