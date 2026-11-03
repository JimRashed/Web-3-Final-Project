using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace QuizArena.ArchitectureTests;

public sealed class ServiceBoundaryTests
{
    private static readonly string[] Services =
    [
        "Identity", "Statistics", "QuestionBank", "Lobby", "Gameplay", "Notification"
    ];

    private static readonly string[] Layers = ["Domain", "Data", "Api"];

    [Fact]
    public void ServiceProjects_WhenInspected_RespectP02Boundaries()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var errors = new List<string>();

        // Act
        foreach (var service in Services)
        {
            foreach (var layer in Layers)
            {
                var path = ProjectPath(root, service, layer);
                if (!File.Exists(path))
                {
                    errors.Add($"Missing {path}");
                    continue;
                }

                errors.AddRange(Inspect(XDocument.Load(path), path, root, service, layer));
            }

            errors.AddRange(InspectTestProjects(root, service));
        }

        errors.AddRange(InspectClientProjects(root));

        // Assert
        errors.Should().BeEmpty(string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void DomainProject_WhenNuGetPackageIsAdded_ReportsViolation()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var path = ProjectPath(root, "Identity", "Domain");
        var document = XDocument.Load(path);
        document.Root!.Add(new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", "Microsoft.EntityFrameworkCore"))));

        // Act
        var errors = Inspect(document, path, root, "Identity", "Domain");

        // Assert
        errors.Should().Contain(error => error.Contains("PackageReference", StringComparison.Ordinal));
    }

    [Fact]
    public void ApiProject_WhenPeerProjectIsReferenced_ReportsViolation()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var path = ProjectPath(root, "Identity", "Api");
        var document = XDocument.Load(path);
        document.Root!.Add(new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", @"..\QuizArena.Statistics.Domain\QuizArena.Statistics.Domain.csproj"))));

        // Act
        var errors = Inspect(document, path, root, "Identity", "Api");

        // Assert
        errors.Should().Contain(error => error.Contains("cross-service", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(@"..\QuizArena.Identity.Domain\QuizArena.Identity.Domain.csproj")]
    [InlineData("../QuizArena.Identity.Domain/QuizArena.Identity.Domain.csproj")]
    public void ProjectReferences_WithEitherSeparator_ResolveOwnProject(string include)
    {
        // Arrange
        var root = FindRepositoryRoot();
        var path = ProjectPath(root, "Identity", "Data");
        var project = new XElement("Project", new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", include))));

        // Act
        var reference = ProjectReferences(project, path).Single();

        // Assert
        reference.Should().Be(ProjectPath(root, "Identity", "Domain"));
    }

    [Fact]
    public void DomainProject_WhenWebSdkIsSelected_ReportsViolation()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var path = ProjectPath(root, "Identity", "Domain");
        var document = XDocument.Load(path);
        document.Root!.SetAttributeValue("Sdk", "Microsoft.NET.Sdk.Web");

        // Act
        var errors = Inspect(document, path, root, "Identity", "Domain");

        // Assert
        errors.Should().Contain(error => error.Contains("SDK", StringComparison.Ordinal));
    }

    private static IEnumerable<string> Inspect(XDocument document, string path, string root, string service, string layer)
    {
        var errors = new List<string>();
        var project = document.Root;
        if (project is null)
        {
            return [$"{path}: empty project"];
        }

        var expectedSdk = layer == "Api" ? "Microsoft.NET.Sdk.Web" : "Microsoft.NET.Sdk";
        if ((string?)project.Attribute("Sdk") != expectedSdk)
        {
            errors.Add($"{path}: {layer} SDK must be {expectedSdk}");
        }

        if (project.Descendants("TargetFramework").Select(x => x.Value).SingleOrDefault() != "net10.0")
        {
            errors.Add($"{path}: TargetFramework must be net10.0");
        }

        if (layer == "Domain")
        {
            foreach (var forbidden in new[] { "PackageReference", "FrameworkReference", "Reference", "Import" })
            {
                if (project.Descendants(forbidden).Any())
                {
                    errors.Add($"{path}: Domain must not declare {forbidden}");
                }
            }
        }

        var expected = layer switch
        {
            "Domain" => Array.Empty<string>(),
            "Data" => [ProjectPath(root, service, "Domain")],
            "Api" => [ProjectPath(root, service, "Domain"), ProjectPath(root, service, "Data")],
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };
        var actual = ProjectReferences(project, path).ToArray();
        foreach (var reference in actual)
        {
            if (!reference.Contains($"QuizArena.{service}.", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{path}: cross-service or legacy project reference {reference}");
            }
        }

        if (!actual.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x), StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"{path}: {layer} project references must be exactly [{string.Join(", ", expected)}]");
        }

        if (project.Descendants("Reference").Any())
        {
            errors.Add($"{path}: direct assembly Reference is not allowed");
        }

        return errors;
    }

    private static IEnumerable<string> InspectTestProjects(string root, string service)
    {
        foreach (var (kind, targetLayer) in new[] { ("UnitTests", "Domain"), ("IntegrationTests", "Api") })
        {
            var path = Path.Combine(root, "tests", $"QuizArena.{service}.{kind}", $"QuizArena.{service}.{kind}.csproj");
            if (!File.Exists(path))
            {
                yield return $"Missing {path}";
                continue;
            }

            var document = XDocument.Load(path);
            var references = ProjectReferences(document.Root!, path).ToArray();
            var expected = ProjectPath(root, service, targetLayer);
            if (references.Length != 1 || !string.Equals(references[0], expected, StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{path}: service test project must reference only own {targetLayer}";
            }
        }
    }

    private static IEnumerable<string> InspectClientProjects(string root)
    {
        var clientRoot = Path.Combine(root, "src", "QuizArena.Client");
        if (!Directory.Exists(clientRoot))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(clientRoot, "*.csproj", SearchOption.AllDirectories))
        {
            if (ProjectReferences(XDocument.Load(path).Root!, path).Any())
            {
                yield return $"{path}: frontend must not reference backend projects";
            }
        }
    }

    private static IEnumerable<string> ProjectReferences(XElement project, string path)
    {
        var projectDirectory = Path.GetDirectoryName(path)!;
        return project.Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include)));
    }

    private static string ProjectPath(string root, string service, string layer) =>
        Path.Combine(root, "src", $"QuizArena.{service}.{layer}", $"QuizArena.{service}.{layer}.csproj");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QuizArena.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate QuizArena.sln above the test output directory.");
    }
}
