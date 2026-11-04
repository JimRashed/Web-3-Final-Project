using System.Security.Cryptography;
using QuizArena.Lobby.Domain.Exceptions;

namespace QuizArena.Lobby.Domain.Services;

public static class JoinCodeGenerator
{
    public const int Length = 6;
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>Generates a candidate. Persistence owns uniqueness and collision retries.</summary>
    public static string Generate()
    {
        Span<char> code = stackalloc char[Length];
        for (var index = 0; index < code.Length; index++)
        {
            code[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }

    public static bool IsValid(string? joinCode) =>
        joinCode is { Length: Length } && joinCode.All(character =>
            character is >= 'A' and <= 'Z' or >= '0' and <= '9');

    public static string Validate(string joinCode)
    {
        if (!IsValid(joinCode))
        {
            throw new InvalidLobbyException(nameof(joinCode), "Join code must contain six uppercase ASCII letters or digits.");
        }

        return joinCode;
    }
}
