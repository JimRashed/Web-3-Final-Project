using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QuizArena.Statistics.Api.Security;
using QuizArena.Statistics.IntegrationTests.Security;

namespace QuizArena.Statistics.IntegrationTests.Endpoints;

/// <summary>Small HTTP helpers shared by the Statistics endpoint tests.</summary>
internal static class StatisticsHttp
{
    public static string Player(string subject, string scope = "stats.read leaderboard.read") =>
        TestTokens.Create(ActorKind.User, scope, claims => claims["sub"] = subject);

    public static string Gameplay(string scope = "results.write") => TestTokens.Create(ActorKind.Machine, scope);

    public static string Result(string matchId, string userId, int score, bool won, string playedAt) =>
        JsonSerializer.Serialize(new { matchId, userId, score, won, playedAt });

    public static async Task<HttpResponseMessage> Send(StatisticsApiFactory factory, HttpMethod method, string path, string? token, string? body = null)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    public static JsonElement ReadContract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
        {
            directory = directory.Parent;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "contracts", "openapi", "statistics.yaml")));
        return document.RootElement.Clone();
    }

    public static IReadOnlyList<string> Operations(JsonElement openApi) =>
        openApi.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(op => op.Name is "get" or "put" or "post" or "delete" or "patch")
                .Select(op => $"{op.Name.ToUpperInvariant()} {path.Name}"))
            .ToList();
}
