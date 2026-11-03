namespace QuizArena.Statistics.Domain.Enums;

/// <summary>
/// Outcome of comparing an incoming result with the result already stored under the same key.
/// </summary>
public enum ResultReplayDecision
{
    /// <summary>No result exists for this key; persist the incoming result.</summary>
    New = 0,

    /// <summary>The stored result is identical; succeed without writing.</summary>
    IdempotentReplay = 1,

    /// <summary>The stored result differs; reject with a conflict.</summary>
    Conflict = 2
}
