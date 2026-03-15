using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeUsageMap.Core.Symbols;

public sealed class AdhocWorkspaceLoader : IWorkspaceLoader
{
    public async Task<LoadedSolution> LoadAsync(string solutionPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var solutionDirectory = Path.GetDirectoryName(solutionPath)
            ?? throw new InvalidOperationException($"Failed to resolve solution directory for '{solutionPath}'.");

        var projectPaths = ParseProjectPaths(solutionPath, solutionDirectory);
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectIds = new Dictionary<string, ProjectId>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectPath in projectPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectId = ProjectId.CreateNewId(debugName: projectPath);
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projectName,
                projectName,
                LanguageNames.CSharp,
                filePath: projectPath,
                outputFilePath: null,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
                metadataReferences: GetMetadataReferences());

            solution = solution.AddProject(projectInfo);
            projectIds[projectPath] = projectId;
        }

        foreach (var projectPath in projectPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectId = projectIds[projectPath];
            foreach (var referencePath in ParseProjectReferences(projectPath))
            {
                if (projectIds.TryGetValue(referencePath, out var referencedProjectId))
                {
                    solution = solution.AddProjectReference(projectId, new ProjectReference(referencedProjectId));
                }
            }

            foreach (var sourceFile in EnumerateSourceFiles(projectPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceText = await File.ReadAllTextAsync(sourceFile, cancellationToken);
                solution = solution.AddDocument(
                    DocumentId.CreateNewId(projectId, debugName: sourceFile),
                    Path.GetFileName(sourceFile),
                    SourceText.From(sourceText),
                    filePath: sourceFile);
            }
        }

        workspace.TryApplyChanges(solution);

        return new LoadedSolution
        {
            Workspace = workspace,
            Solution = workspace.CurrentSolution,
        };
    }

    private static IReadOnlyList<string> ParseProjectPaths(string solutionPath, string solutionDirectory)
    {
        var result = new List<string>();

        foreach (var line in File.ReadLines(solutionPath))
        {
            var marker = ".csproj";
            var markerIndex = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var startQuote = line.LastIndexOf('"', markerIndex);
            if (startQuote < 0)
            {
                continue;
            }

            var endQuote = line.IndexOf('"', markerIndex);
            if (endQuote < 0 || endQuote <= startQuote)
            {
                continue;
            }

            var relativePath = line[(startQuote + 1)..endQuote];
            var fullPath = Path.GetFullPath(Path.Combine(solutionDirectory, NormalizeRelativePath(relativePath)));
            if (!result.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(fullPath);
            }
        }

        return result;
    }

    private static IEnumerable<string> ParseProjectReferences(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var document = XDocument.Load(projectPath);

        foreach (var element in document.Descendants().Where(static item => item.Name.LocalName == "ProjectReference"))
        {
            var include = element.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            yield return Path.GetFullPath(Path.Combine(projectDirectory, NormalizeRelativePath(include)));
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;

        return Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                references.Add(path);
            }
        }

        foreach (var path in GetRoslynReferencePaths())
        {
            references.Add(path);
        }

        return references
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static IEnumerable<string> GetRoslynReferencePaths()
    {
        var assemblies = new[]
        {
            typeof(Compilation).Assembly,
            typeof(CSharpCompilation).Assembly,
            typeof(Workspace).Assembly,
            typeof(CSharpSyntaxTree).Assembly,
        };

        foreach (var assembly in assemblies)
        {
            if (!string.IsNullOrWhiteSpace(assembly.Location))
            {
                yield return assembly.Location;
            }
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        return path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }
}
