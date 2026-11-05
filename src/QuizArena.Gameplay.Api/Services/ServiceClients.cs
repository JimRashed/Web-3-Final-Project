using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using QuizArena.Gameplay.Domain.Entities;
using QuizArena.Gameplay.Domain.Exceptions;

namespace QuizArena.Gameplay.Api.Services;

/// <summary>
/// Typed HTTP adapter for the Lobby service's internal verification route
/// (<c>GET /internal/lobbies/{joinCode}</c>). Uses the machine token for <c>lobbies.verify</c>;
/// the caller's own token is never forwarded.
/// </summary>
public sealed class LobbyHttpGateway(
    HttpClient http,
    IMachineTokenSource tokens,
    IOptions<GameplayServiceOptions> options,
    ILogger<LobbyHttpGateway> logger) : ILobbyGateway
{
    public async Task<LobbyState?> GetAsync(string joinCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            throw new GameplayRequestException(StatusCodes.Status400BadRequest, "A join code is required.");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                Endpoint($"internal/lobbies/{Uri.EscapeDataString(joinCode)}"));
            await tokens.WithMachineTokenAsync(request, GameplayServiceOptions.Scopes.LobbyVerify, cancellationToken);

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Our own credential was rejected: a configuration fault, not an unknown lobby.
                logger.LogError("The Lobby service rejected the Gameplay machine token (HTTP {Status}).", (int)response.StatusCode);
                throw Unavailable("The lobby service rejected the service credential.");
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("The Lobby service failed the verification call (HTTP {Status}).", (int)response.StatusCode);
                throw Unavailable("The lobby service is unavailable.");
            }

            var payload = await response.Content.ReadFromJsonAsync<LobbyPayload>(cancellationToken)
                ?? throw Unavailable("The lobby service returned an empty verification response.");

            return ToState(payload);
        }
        catch (Exception exception) when ((exception is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "The Lobby service call failed at the transport level.");
            throw Unavailable("The lobby service is unavailable.");
        }
        catch (MachineTokenUnavailableException exception)
        {
            logger.LogError(exception, "No machine token is available for the Lobby service.");
            throw Unavailable("The lobby service credential is not available.");
        }
    }

    private Uri Endpoint(string path)
    {
        if (!Uri.TryCreate(options.Value.Lobby, UriKind.Absolute, out var baseUri))
        {
            throw Unavailable("The lobby service address is not configured (ServiceUrls:Lobby).");
        }

        return new Uri(baseUri, path);
    }

    private static LobbyState ToState(LobbyPayload payload)
    {
        if (payload.LobbyId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payload.HostUserId) ||
            payload.PlayerIds is null ||
            payload.PlayerIds.Count == 0)
        {
            throw Unavailable("The lobby service returned an incomplete lobby.");
        }

        return new LobbyState(
            payload.LobbyId.ToString(),
            payload.HostUserId,
            payload.PlayerIds,
            string.Equals(payload.Status, "Locked", StringComparison.OrdinalIgnoreCase));
    }

    private static GameplayRequestException Unavailable(string message) =>
        new(StatusCodes.Status503ServiceUnavailable, message);

    /// <summary>Wire shape of the Lobby service's internal response (camelCase JSON, web defaults).</summary>
    private sealed record LobbyPayload(Guid LobbyId, string JoinCode, string Status, string HostUserId, IReadOnlyList<string> PlayerIds);
}

/// <summary>
/// Typed HTTP adapter for the QuestionBank service's internal snapshot route
/// (<c>POST /internal/question-sets</c>, body <c>{ count }</c>). The answer is carried inside the
/// snapshot and stays in the Gameplay domain; it is never returned on a public route.
/// </summary>
public sealed class QuestionBankHttpClient(
    HttpClient http,
    IMachineTokenSource tokens,
    IOptions<GameplayServiceOptions> options,
    ILogger<QuestionBankHttpClient> logger) : IQuestionCatalog
{
    public async Task<IReadOnlyList<QuestionSnapshot>> SnapshotAsync(int count, CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            throw new GameplayRequestException(StatusCodes.Status400BadRequest, "A positive question count is required.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("internal/question-sets"))
            {
                Content = JsonContent.Create(new { count }),
            };
            await tokens.WithMachineTokenAsync(request, GameplayServiceOptions.Scopes.QuestionsPlay, cancellationToken);

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // 409 means the catalog cannot supply the exact count; either way no match may start.
                logger.LogError("The QuestionBank service failed the snapshot call (HTTP {Status}).", (int)response.StatusCode);
                throw Unavailable("The question bank cannot supply a question set right now.");
            }

            var payload = await response.Content.ReadFromJsonAsync<QuestionSetPayload[]>(cancellationToken)
                ?? throw Unavailable("The question bank returned an empty question set.");

            return Materialize(payload, count);
        }
        catch (Exception exception) when ((exception is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "The QuestionBank service call failed at the transport level.");
            throw Unavailable("The question bank is unavailable.");
        }
        catch (MachineTokenUnavailableException exception)
        {
            logger.LogError(exception, "No machine token is available for the QuestionBank service.");
            throw Unavailable("The question bank credential is not available.");
        }
    }

    private Uri Endpoint(string path)
    {
        if (!Uri.TryCreate(options.Value.QuestionBank, UriKind.Absolute, out var baseUri))
        {
            throw Unavailable("The question bank service address is not configured (ServiceUrls:QuestionBank).");
        }

        return new Uri(baseUri, path);
    }

    private static IReadOnlyList<QuestionSnapshot> Materialize(QuestionSetPayload[] payload, int count)
    {
        if (payload.Length != count)
        {
            throw Unavailable("The question bank returned the wrong number of questions.");
        }

        var snapshots = new QuestionSnapshot[count];
        for (var index = 0; index < payload.Length; index++)
        {
            var question = payload[index];
            try
            {
                snapshots[index] = QuestionSnapshot.Create(
                    question.Id,
                    question.Prompt,
                    question.Options,
                    question.CorrectAnswer);
            }
            catch (InvalidMatchException exception)
            {
                // A malformed snapshot must fail the match, not create a match with a question the
                // service cannot grade.
                throw Unavailable($"The question bank returned a malformed question: {exception.Message}");
            }
        }

        if (snapshots.Select(snapshot => snapshot.QuestionId).Distinct().Count() != count)
        {
            throw Unavailable("The question bank returned duplicate questions.");
        }

        return snapshots;
    }

    private static GameplayRequestException Unavailable(string message) =>
        new(StatusCodes.Status503ServiceUnavailable, message);

    /// <summary>Wire shape of the QuestionBank internal snapshot response.</summary>
    private sealed record QuestionSetPayload(
        Guid Id,
        string Prompt,
        string Category,
        IReadOnlyList<string> Options,
        string CorrectAnswer);
}
