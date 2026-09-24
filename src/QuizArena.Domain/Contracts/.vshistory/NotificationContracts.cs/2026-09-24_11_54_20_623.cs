namespace QuizArena.Domain.Contracts;

public record CreateEventRequest(string Type, string UserId, string PayloadJson);

public record EventResponse(
    int Id,
    string Type,
    string UserId,
    string PayloadJson,
    DateTime CreatedAt);

public record UpdatePrefRequest(bool EmailEnabled, bool PushEnabled);