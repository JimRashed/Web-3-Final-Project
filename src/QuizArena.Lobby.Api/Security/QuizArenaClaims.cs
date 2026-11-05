namespace QuizArena.Lobby.Api.Security;

public static class QuizArenaClaims
{
    public const string Issuer = "iss";
    public const string Subject = "sub";
    public const string AuthorizedParty = "azp";
    public const string Actor = "qa_actor";
    public const string Scope = "scope";
    public const string ScopeList = "scp";
    public const string TokenType = "typ";

    public const string UserActor = "user";
    public const string MachineActor = "machine";
    public const string AccessTokenType = "Bearer";
}
