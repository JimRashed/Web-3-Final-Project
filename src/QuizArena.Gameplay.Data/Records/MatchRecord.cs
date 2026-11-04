using QuizArena.Gameplay.Domain.Enums;

namespace QuizArena.Gameplay.Data.Records;

/// <summary>
/// Persistence representation of the <see cref="Domain.Entities.Match"/> aggregate.
/// The aggregate is loaded and saved as a whole: question snapshots and answers are stored as
/// canonical JSON documents because they are only ever read in the context of their match.
/// </summary>
public sealed class MatchRecord
{
    public string MatchId { get; set; } = string.Empty;

    public string LobbyId { get; set; } = string.Empty;

    public string PlayerAId { get; set; } = string.Empty;

    public string PlayerBId { get; set; } = string.Empty;

    public MatchStatus Status { get; set; }

    public int PlayerAScore { get; set; }

    public int PlayerBScore { get; set; }

    public int CurrentQuestionIndex { get; set; }

    public string? WinnerUserId { get; set; }

    public bool Draw { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Five immutable question snapshots, canonical JSON.</summary>
    public string QuestionsJson { get; set; } = "[]";

    /// <summary>Immutable answers, canonical JSON.</summary>
    public string AnswersJson { get; set; } = "[]";

    /// <summary>
    /// SQL Server rowversion. Two players answering the same round concurrently would otherwise
    /// overwrite each other's answer (last write wins); the token turns that into a detectable conflict.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
