using FluentAssertions;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Enums;
using QuizArena.Gameplay.Domain.Exceptions;
using Xunit;

namespace QuizArena.Gameplay.UnitTests;

public sealed class MatchTests
{
    private const string PlayerA = "player-a";
    private const string PlayerB = "player-b";
    private const string MatchId = "match-1";
    private const string LobbyId = "lobby-1";

    private static readonly DateTime CreatedAt = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Guid[] QuestionIds =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555")
    ];

    private static QuestionSnapshot Snapshot(Guid questionId) =>
        QuestionSnapshot.Create(questionId, $"Question {questionId}", ["A", "B", "C", "D"], "A");

    private static List<QuestionSnapshot> Snapshots() =>
        QuestionIds.Select(Snapshot).ToList();

    private static Match NewMatch() =>
        Match.Create(MatchId, LobbyId, PlayerA, PlayerB, Snapshots(), CreatedAt);

    [Fact]
    public void Create_WithFiveDistinctQuestions_StartsActiveAtFirstQuestion()
    {
        // Act
        var match = NewMatch();

        // Assert
        match.Status.Should().Be(MatchStatus.Active);
        match.CurrentQuestionIndex.Should().Be(0);
        match.PlayerAScore.Should().Be(0);
        match.PlayerBScore.Should().Be(0);
        match.CurrentQuestion!.QuestionId.Should().Be(QuestionIds[0]);
        match.Answers.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithWrongQuestionCount_ThrowsInvalidMatchException()
    {
        // Act
        var act = () => Match.Create(MatchId, LobbyId, PlayerA, PlayerB, Snapshots().Take(4).ToList(), CreatedAt);

        // Assert
        act.Should().Throw<InvalidMatchException>();
    }

    [Fact]
    public void Create_WithDuplicateQuestionIds_ThrowsInvalidMatchException()
    {
        // Arrange
        var questions = Snapshots();
        questions[4] = Snapshot(QuestionIds[0]);

        // Act
        var act = () => Match.Create(MatchId, LobbyId, PlayerA, PlayerB, questions, CreatedAt);

        // Assert
        act.Should().Throw<InvalidMatchException>();
    }

    [Fact]
    public void Create_WithSamePlayerOnBothSides_ThrowsInvalidMatchException()
    {
        // Act
        var act = () => Match.Create(MatchId, LobbyId, PlayerA, PlayerA, Snapshots(), CreatedAt);

        // Assert
        act.Should().Throw<InvalidMatchException>();
    }

    [Fact]
    public void SubmitAnswer_ByOutsider_ThrowsAccessDenied()
    {
        // Arrange
        var match = NewMatch();

        // Act
        var act = () => match.SubmitAnswer("intruder", QuestionIds[0], "A", CreatedAt);

        // Assert
        act.Should().Throw<MatchAccessDeniedException>();
    }

    [Fact]
    public void SubmitAnswer_WithCorrectAnswer_IncreasesScore()
    {
        // Arrange
        var match = NewMatch();

        // Act
        var outcome = match.SubmitAnswer(PlayerA, QuestionIds[0], "A", CreatedAt);

        // Assert
        outcome.Correct.Should().BeTrue();
        outcome.PlayerAScore.Should().Be(1);
        outcome.PlayerBScore.Should().Be(0);
        match.PlayerAScore.Should().Be(1);
    }

    [Fact]
    public void SubmitAnswer_WithWrongAnswer_DoesNotIncreaseScore()
    {
        // Arrange
        var match = NewMatch();

        // Act
        var outcome = match.SubmitAnswer(PlayerB, QuestionIds[0], "B", CreatedAt);

        // Assert
        outcome.Correct.Should().BeFalse();
        outcome.PlayerBScore.Should().Be(0);
    }

    [Fact]
    public void SubmitAnswer_AdvancesOnlyAfterBothPlayersAnswer()
    {
        // Arrange
        var match = NewMatch();

        // Act
        var first = match.SubmitAnswer(PlayerA, QuestionIds[0], "A", CreatedAt);
        var indexAfterFirst = match.CurrentQuestionIndex;
        var second = match.SubmitAnswer(PlayerB, QuestionIds[0], "A", CreatedAt);

        // Assert
        first.RoundComplete.Should().BeFalse();
        indexAfterFirst.Should().Be(0);
        second.RoundComplete.Should().BeTrue();
        match.CurrentQuestionIndex.Should().Be(1);
    }

    [Fact]
    public void SubmitAnswer_TwiceInSameRound_ThrowsConflict()
    {
        // Arrange
        var match = NewMatch();
        match.SubmitAnswer(PlayerA, QuestionIds[0], "A", CreatedAt);

        // Act
        var act = () => match.SubmitAnswer(PlayerA, QuestionIds[0], "A", CreatedAt);

        // Assert
        act.Should().Throw<MatchConflictException>()
            .Which.Reason.Should().Be(MatchConflictReason.DuplicateAnswer);
    }

    [Fact]
    public void SubmitAnswer_ForStaleQuestion_ThrowsConflict()
    {
        // Arrange
        var match = NewMatch();

        // Act
        var act = () => match.SubmitAnswer(PlayerA, QuestionIds[1], "A", CreatedAt);

        // Assert
        act.Should().Throw<MatchConflictException>()
            .Which.Reason.Should().Be(MatchConflictReason.StaleQuestion);
    }

    [Fact]
    public void SubmitAnswer_WithAnswerOutsideOptions_ThrowsInvalidMatchException()
    {
        // Arrange
        var match = NewMatch();

        // Act
        var act = () => match.SubmitAnswer(PlayerA, QuestionIds[0], "Z", CreatedAt);

        // Assert
        act.Should().Throw<InvalidMatchException>();
    }

    [Fact]
    public void SubmitAnswer_AfterFiveCompleteRounds_FinishesWithWinner()
    {
        // Arrange
        var match = NewMatch();
        AnswerOutcome? last = null;

        // Act
        for (var round = 0; round < QuestionIds.Length; round++)
        {
            match.SubmitAnswer(PlayerA, QuestionIds[round], "A", CreatedAt);
            last = match.SubmitAnswer(PlayerB, QuestionIds[round], "B", CreatedAt);
        }

        // Assert
        last!.Finished.Should().BeTrue();
        match.Status.Should().Be(MatchStatus.Finished);
        match.Draw.Should().BeFalse();
        match.WinnerUserId.Should().Be(PlayerA);
        match.PlayerAScore.Should().Be(5);
        match.PlayerBScore.Should().Be(0);
        match.CompletedAt.Should().Be(CreatedAt);
        match.CurrentQuestion.Should().BeNull();
        match.Answers.Should().HaveCount(10);
    }

    [Fact]
    public void SubmitAnswer_WhenBothScoreEqually_FinishesAsDraw()
    {
        // Arrange
        var match = NewMatch();

        // Act
        for (var round = 0; round < QuestionIds.Length; round++)
        {
            match.SubmitAnswer(PlayerA, QuestionIds[round], "A", CreatedAt);
            match.SubmitAnswer(PlayerB, QuestionIds[round], "A", CreatedAt);
        }

        // Assert
        match.Status.Should().Be(MatchStatus.Finished);
        match.Draw.Should().BeTrue();
        match.WinnerUserId.Should().BeNull();
    }

    [Fact]
    public void SubmitAnswer_OnFinishedMatch_ThrowsConflict()
    {
        // Arrange
        var match = NewMatch();
        for (var round = 0; round < QuestionIds.Length; round++)
        {
            match.SubmitAnswer(PlayerA, QuestionIds[round], "A", CreatedAt);
            match.SubmitAnswer(PlayerB, QuestionIds[round], "A", CreatedAt);
        }

        // Act
        var act = () => match.SubmitAnswer(PlayerA, QuestionIds[4], "A", CreatedAt);

        // Assert
        act.Should().Throw<MatchConflictException>()
            .Which.Reason.Should().Be(MatchConflictReason.MatchFinished);
    }

    [Fact]
    public void CurrentQuestion_ReadTwice_DoesNotAdvanceState()
    {
        // Arrange
        var match = NewMatch();

        // Act
        var first = match.CurrentQuestion;
        var second = match.CurrentQuestion;

        // Assert
        first.Should().BeSameAs(second);
        match.CurrentQuestionIndex.Should().Be(0);
    }

    [Fact]
    public void Answers_RecordRoundIndexAndPlayer()
    {
        // Arrange
        var match = NewMatch();

        // Act
        match.SubmitAnswer(PlayerA, QuestionIds[0], "A", CreatedAt);

        // Assert
        match.Answers.Should().ContainSingle();
        var answer = match.Answers[0];
        answer.RoundIndex.Should().Be(0);
        answer.UserId.Should().Be(PlayerA);
        answer.Correct.Should().BeTrue();
    }
}
