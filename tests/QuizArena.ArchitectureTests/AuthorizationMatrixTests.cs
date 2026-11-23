using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace QuizArena.ArchitectureTests;

/// <summary>
/// The security matrix as data (#142, SEC-B3): every operation in every committed contract declares a
/// policy, an actor and an audience; each declaration exists in that service's policy table with the
/// same actor kind; anonymous operations say so explicitly; and no policy is declared without an
/// operation that uses it. This runs on every build, so a route cannot quietly ship unauthenticated or
/// with a scope nobody grants.
/// </summary>
public sealed partial class AuthorizationMatrixTests
{
    public static TheoryData<string> Services() => new()
    {
        "identity", "statistics", "questionbank", "lobby", "gameplay", "notification",
    };

    [Theory]
    [MemberData(nameof(Services))]
    public void EveryOperationDeclaresAPolicyThatTheServiceActuallyGrants(string service)
    {
        // Arrange
        var root = FindRepositoryRoot();
        var policies = ReadPolicies(Path.Combine(root, "src", $"QuizArena.{Pascal(service)}.Api", "Security", $"{Pascal(service)}Policies.cs"));
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "contracts", "openapi", $"{service}.yaml")));

        var problems = new List<string>();
        var used = new HashSet<string>(StringComparer.Ordinal);

        // Act
        foreach (var operation in Operations(contract))
        {
            var id = $"{operation.Method.ToUpperInvariant()} {operation.Path}";

            if (operation.Scope is null)
            {
                // Anonymous is allowed only when the document says so twice: x-policy "public" and an
                // explicitly empty security list. Anything else is an undeclared protection gap.
                if (!operation.DeclaredPublic)
                {
                    problems.Add($"{id} declares no x-policy and is not public");
                }
                else if (!operation.SecurityEmpty)
                {
                    problems.Add($"{id} is public but its security list is not explicitly empty");
                }

                continue;
            }

            used.Add(operation.Scope);

            if (!policies.TryGetValue(operation.Scope, out var actor))
            {
                problems.Add($"{id} uses scope '{operation.Scope}' that {Pascal(service)}Policies does not declare");
                continue;
            }

            if (!string.Equals(actor, operation.Actor, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{id} declares actor '{operation.Actor}' but the policy grants '{actor}'");
            }

            if (!string.Equals(operation.Audience, $"quizarena-{service}", StringComparison.Ordinal))
            {
                problems.Add($"{id} declares audience '{operation.Audience}', expected 'quizarena-{service}'");
            }
        }

        // Assert
        problems.Should().BeEmpty(string.Join(Environment.NewLine, problems));

        var unused = policies.Keys.Except(used, StringComparer.Ordinal).ToArray();
        unused.Should().BeEmpty($"every declared policy must protect at least one operation; unused: {string.Join(", ", unused)}");
    }

    [Fact]
    public void TheAuthorizationMatrixDocumentListsEveryPolicyName()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var document = File.ReadAllText(Path.Combine(root, "docs", "milestone3", "authorization-matrix.md"));
        var declared = new List<string>();

        foreach (var service in new[] { "identity", "statistics", "questionbank", "lobby", "gameplay", "notification" })
        {
            var policies = ReadPolicies(Path.Combine(root, "src", $"QuizArena.{Pascal(service)}.Api", "Security", $"{Pascal(service)}Policies.cs"));
            declared.AddRange(policies.Keys);
        }

        // Act
        var missing = declared.Where(scope => !document.Contains(scope, StringComparison.Ordinal)).ToArray();

        // Assert
        missing.Should().BeEmpty($"every scope must appear in the authorization matrix; missing: {string.Join(", ", missing)}");
    }

    private sealed record Operation(string Path, string Method, string? Scope, string? Actor, string? Audience, bool DeclaredPublic, bool SecurityEmpty);

    private static IEnumerable<Operation> Operations(JsonDocument contract)
    {
        foreach (var path in contract.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var verb in path.Value.EnumerateObject())
            {
                // Path-level members such as `parameters` are not operations.
                if (verb.Value.ValueKind != JsonValueKind.Object) continue;

                var declaredPublic = verb.Value.TryGetProperty("x-policy", out var policy)
                    && string.Equals(policy.GetString(), "public", StringComparison.Ordinal);
                var securityEmpty = verb.Value.TryGetProperty("security", out var security)
                    && security.ValueKind == JsonValueKind.Array
                    && security.GetArrayLength() == 0;

                yield return declaredPublic
                    ? new Operation(path.Name, verb.Name, null, null, null, true, securityEmpty)
                    : new Operation(
                        path.Name,
                        verb.Name,
                        policy.ValueKind == JsonValueKind.String ? policy.GetString() : null,
                        verb.Value.TryGetProperty("x-actor", out var actor) ? actor.GetString() : "(missing)",
                        verb.Value.TryGetProperty("x-audience", out var audience) ? audience.GetString() : "(missing)",
                        false,
                        securityEmpty);
            }
        }
    }

    private static Dictionary<string, string> ReadPolicies(string path)
    {
        var text = File.ReadAllText(path);
        var constants = ConstantPattern().Matches(text)
            .ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value, StringComparer.Ordinal);

        var actors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match entry in EntryPattern().Matches(text))
        {
            var name = entry.Groups["name"].Value;
            if (!constants.TryGetValue(name, out var scope))
            {
                throw new InvalidOperationException($"{path}: policy entry '{name}' has no matching constant.");
            }

            actors[scope] = entry.Groups["actor"].Value;
        }

        return actors;
    }

    private static string Pascal(string service) =>
        service switch
        {
            "identity" => "Identity",
            "statistics" => "Statistics",
            "questionbank" => "QuestionBank",
            "lobby" => "Lobby",
            "gameplay" => "Gameplay",
            "notification" => "Notification",
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null),
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("QuizArena.sln not found.");
    }

    [GeneratedRegex("""public const string (?<name>\w+) = "(?<value>[^"]+)";""")]
    private static partial Regex ConstantPattern();

    [GeneratedRegex(@"\[(?<name>\w+)\] = ActorKind\.(?<actor>\w+)")]
    private static partial Regex EntryPattern();
}
