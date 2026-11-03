namespace QuizArena.Gameplay.Api.Security;

public sealed class QuizArenaAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string BrowserClientId { get; set; } = string.Empty;

    public string MachineClientId { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;
}
