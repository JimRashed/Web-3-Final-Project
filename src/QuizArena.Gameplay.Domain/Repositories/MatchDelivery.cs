namespace QuizArena.Gameplay.Domain.Repositories;

/// <summary>
/// A message that must reach another service after the match transaction commits. It is persisted
/// in the same unit of work as the match, so a committed match can never lose a required delivery
/// and a rolled-back one can never announce a match that does not exist.
/// </summary>
/// <param name="Destination">Logical receiver: <see cref="Statistics"/> or <see cref="Notification"/>.</param>
/// <param name="Operation">Wire operation version, for example <c>results.record.v1</c>.</param>
/// <param name="PayloadJson">Immutable canonical body; never rebuilt after the commit.</param>
/// <param name="IdempotencyKey">
/// Receiver-facing key. Replays carry the same key, which is what lets the receiver deduplicate.
/// </param>
public sealed record MatchDelivery(
    string Destination,
    string Operation,
    string PayloadJson,
    string IdempotencyKey)
{
    public const string Statistics = "statistics";

    public const string Notification = "notification";
}
