using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizArena.Gameplay.Data.Context;
using QuizArena.Gameplay.Data.Records;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Repositories;

namespace QuizArena.Gameplay.Data.Repositories;

public sealed class MatchRepository(GameplayDbContext context) : IMatchRepository
{
    // SQL Server duplicate-key error numbers (unique index / primary key).
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Match?> FindAsync(string matchId, CancellationToken cancellationToken)
    {
        var record = await context.Matches
            .AsNoTracking()
            .SingleOrDefaultAsync(stored => stored.MatchId == matchId, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    public async Task SaveAsync(Match match, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(match);

        var record = await context.Matches
            .SingleOrDefaultAsync(stored => stored.MatchId == match.MatchId, cancellationToken);

        var isNew = record is null;
        record ??= new MatchRecord { MatchId = match.MatchId };

        Apply(match, record);

        if (isNew)
        {
            context.Matches.Add(record);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // A concurrent request inserted the same match first: reload and apply on top.
            context.Entry(record).State = EntityState.Detached;
            var winner = await context.Matches
                .SingleOrDefaultAsync(stored => stored.MatchId == match.MatchId, cancellationToken)
                ?? throw new InvalidOperationException("Match insert conflicted but no stored match was found.", exception);

            Apply(match, winner);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static void Apply(Match match, MatchRecord record)
    {
        record.LobbyId = match.LobbyId;
        record.PlayerAId = match.PlayerAId;
        record.PlayerBId = match.PlayerBId;
        record.Status = match.Status;
        record.PlayerAScore = match.PlayerAScore;
        record.PlayerBScore = match.PlayerBScore;
        record.CurrentQuestionIndex = match.CurrentQuestionIndex;
        record.WinnerUserId = match.WinnerUserId;
        record.Draw = match.Draw;
        record.CreatedAt = match.CreatedAt;
        record.CompletedAt = match.CompletedAt;
        record.QuestionsJson = JsonSerializer.Serialize(
            match.Questions.Select(QuestionPayload.FromDomain).ToArray(), JsonOptions);
        record.AnswersJson = JsonSerializer.Serialize(
            match.Answers.Select(AnswerPayload.FromDomain).ToArray(), JsonOptions);
    }

    private static Match ToDomain(MatchRecord record)
    {
        var questions = JsonSerializer.Deserialize<QuestionPayload[]>(record.QuestionsJson, JsonOptions)
            ?? throw new InvalidOperationException($"Match '{record.MatchId}' has no question snapshots.");

        var answers = JsonSerializer.Deserialize<AnswerPayload[]>(record.AnswersJson, JsonOptions)
            ?? [];

        return Match.Rehydrate(
            record.MatchId,
            record.LobbyId,
            record.PlayerAId,
            record.PlayerBId,
            record.Status,
            record.PlayerAScore,
            record.PlayerBScore,
            record.CurrentQuestionIndex,
            record.WinnerUserId,
            record.Draw,
            record.CreatedAt,
            record.CompletedAt,
            questions.Select(payload => payload.ToDomain()).ToArray(),
            answers.Select(payload => payload.ToDomain(record.MatchId)).ToArray());
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or PrimaryKeyViolation };

    private sealed record QuestionPayload(Guid QuestionId, string Text, string[] Options, string CorrectAnswer)
    {
        public static QuestionPayload FromDomain(QuestionSnapshot snapshot) =>
            new(snapshot.QuestionId, snapshot.Text, [.. snapshot.Options], snapshot.CorrectAnswer);

        public QuestionSnapshot ToDomain() =>
            QuestionSnapshot.Create(QuestionId, Text, Options, CorrectAnswer);
    }

    private sealed record AnswerPayload(
        int RoundIndex,
        string UserId,
        Guid QuestionId,
        string Answer,
        bool Correct,
        DateTime AnsweredAt)
    {
        public static AnswerPayload FromDomain(RoundAnswer answer) =>
            new(answer.RoundIndex, answer.UserId, answer.QuestionId, answer.Answer, answer.Correct, answer.AnsweredAt);

        public RoundAnswer ToDomain(string matchId) =>
            new(matchId, RoundIndex, UserId, QuestionId, Answer, Correct, AnsweredAt);
    }
}
