using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeUsageMap.Core.Symbols
{

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
                metadataReferences: GetMetadataReferences(projectPath));

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

                cancellationToken.ThrowIfCancellationRequested();
                var sourceText = File.ReadAllText(sourceFile);
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

            var relativePath = line.Substring(startQuote + 1, endQuote - startQuote - 1);
            var fullPath = Path.GetFullPath(Path.Combine(solutionDirectory, NormalizeRelativePath(relativePath)));
            if (!result.Any(existingPath => string.Equals(existingPath, fullPath, StringComparison.OrdinalIgnoreCase)))
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
            .Where(static path => path.IndexOf($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) < 0)
            .Where(static path => path.IndexOf($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) < 0);
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences(string projectPath)
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (var path in trustedPlatformAssemblies.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                references.Add(path);
            }
        }

        foreach (var path in GetRoslynReferencePaths())
        {
            references.Add(path);
        }

        foreach (var path in ParseMetadataReferencePaths(projectPath))
        {
            references.Add(path);
        }

        return references
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static IEnumerable<string> ParseMetadataReferencePaths(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var document = XDocument.Load(projectPath);

        foreach (var element in document.Descendants().Where(static item => item.Name.LocalName == "Reference"))
        {
            var hintPath = element.Elements().FirstOrDefault(static item => item.Name.LocalName == "HintPath")?.Value;
            if (string.IsNullOrWhiteSpace(hintPath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, NormalizeRelativePath(hintPath)));
            if (File.Exists(fullPath))
            {
                yield return fullPath;
            }
        }
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
}
