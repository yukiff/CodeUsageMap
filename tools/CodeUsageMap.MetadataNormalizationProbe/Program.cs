using System.Collections.Immutable;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Core.References;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

var libraryDllPath = Path.GetFullPath("tests/CodeUsageMap.Integration.Tests/bin/Debug/net9.0/CodeUsageMap.Integration.Tests.dll");
var librarySourcePath = Path.GetFullPath("tests/CodeUsageMap.Integration.Tests/OutgoingDepthSamples.cs");

if (!File.Exists(libraryDllPath))
{
    Console.WriteLine("LIBRARY_DLL_MISSING");
    return;
}

var normalizedCall = await CollectRunCallAsync(includeSourceProject: true);
if (normalizedCall is null)
{
    Console.WriteLine("NORMALIZATION_FAILED:RUN_CALL_MISSING");
    return;
}

Console.WriteLine($"Run project: {normalizedCall.ProjectName}");
Console.WriteLine($"Run file: {normalizedCall.FilePath}");
Console.WriteLine($"Run origin: {normalizedCall.SymbolOrigin}");
Console.WriteLine($"Run normalizedFromMetadata: {normalizedCall.NormalizedFromMetadata}");
Console.WriteLine($"Run normalizationStrategy: {normalizedCall.NormalizationStrategy}");

var fileMatches = string.Equals(normalizedCall.FilePath, librarySourcePath, StringComparison.Ordinal);
var originMatches = string.Equals(normalizedCall.SymbolOrigin, "source", StringComparison.Ordinal);
var normalizedFlagMatches = normalizedCall.NormalizedFromMetadata;
if (!fileMatches)
{
    Console.WriteLine("NORMALIZATION_FAILED:FILE_PATH_MISMATCH");
    return;
}

if (!originMatches)
{
    Console.WriteLine("NORMALIZATION_FAILED:SYMBOL_ORIGIN_MISMATCH");
    return;
}

if (!normalizedFlagMatches)
{
    Console.WriteLine("NORMALIZATION_FAILED:NORMALIZED_FLAG_MISMATCH");
    return;
}

var unresolvedCall = await CollectRunCallAsync(includeSourceProject: true, includeMatchingSourceSymbol: false);
if (unresolvedCall is null)
{
    Console.WriteLine("LIMITATION_FAILED:RUN_CALL_MISSING");
    return;
}

Console.WriteLine($"Unresolved origin: {unresolvedCall.SymbolOrigin}");
Console.WriteLine($"Unresolved limitation: {unresolvedCall.Limitation}");
Console.WriteLine($"Unresolved excludedFromGraph: {unresolvedCall.ExcludedFromGraph}");

if (!string.Equals(unresolvedCall.SymbolOrigin, "unresolved_binary_reference", StringComparison.Ordinal))
{
    Console.WriteLine("LIMITATION_FAILED:SYMBOL_ORIGIN_MISMATCH");
    return;
}

if (!string.Equals(unresolvedCall.Limitation, "source_not_resolved_from_binary_reference", StringComparison.Ordinal))
{
    Console.WriteLine("LIMITATION_FAILED:LIMITATION_MISMATCH");
    return;
}

if (!unresolvedCall.ExcludedFromGraph)
{
    Console.WriteLine("LIMITATION_FAILED:EXCLUDED_FROM_GRAPH_MISMATCH");
    return;
}

Console.WriteLine("NORMALIZATION_CONFIRMED");

async Task<OutgoingCallInfo?> CollectRunCallAsync(bool includeSourceProject, bool includeMatchingSourceSymbol = true)
{
    var workspace = new AdhocWorkspace();
    var solution = workspace.CurrentSolution;

    var consumerProjectId = ProjectId.CreateNewId("ConsumerProject");
    solution = solution.AddProject(ProjectInfo.Create(
        consumerProjectId,
        VersionStamp.Create(),
        "ConsumerProject",
        "ConsumerProject",
        LanguageNames.CSharp,
        filePath: "ConsumerProject.csproj",
        compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
        parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
        metadataReferences: GetPlatformReferences().Add(MetadataReference.CreateFromFile(libraryDllPath))));

    if (includeSourceProject)
    {
        var sourceProjectId = ProjectId.CreateNewId("SourceProject");
        solution = solution.AddProject(ProjectInfo.Create(
            sourceProjectId,
            VersionStamp.Create(),
            "CodeUsageMap.Integration.Tests",
            "CodeUsageMap.Integration.Tests",
            LanguageNames.CSharp,
            filePath: "SourceProject.csproj",
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            metadataReferences: GetPlatformReferences()));

        var sourceText = includeMatchingSourceSymbol
            ? File.ReadAllText(librarySourcePath)
            : """
using System;

namespace CodeUsageMap.Integration.Tests.OutgoingSamples;

public sealed class PipelineStep
{
    public void NotRun()
    {
        Console.WriteLine("mismatch");
    }
}
""";

        solution = solution.AddDocument(
            DocumentId.CreateNewId(sourceProjectId),
            "OutgoingDepthSamples.cs",
            SourceText.From(sourceText),
            filePath: librarySourcePath);
    }

    const string consumerSource = """
using CodeUsageMap.Integration.Tests.OutgoingSamples;

namespace ConsumerProject;

public sealed class Consumer
{
    public void Execute()
    {
        var step = new PipelineStep();
        step.Run();
    }
}
""";

    solution = solution.AddDocument(
        DocumentId.CreateNewId(consumerProjectId),
        "Consumer.cs",
        SourceText.From(consumerSource),
        filePath: Path.GetFullPath("tools/CodeUsageMap.MetadataNormalizationProbe/Consumer.cs"));

    workspace.TryApplyChanges(solution);

    var consumerProject = workspace.CurrentSolution.GetProject(consumerProjectId)!;
    var consumerDocument = consumerProject.Documents.Single();
    var syntaxRoot = await consumerDocument.GetSyntaxRootAsync();
    var semanticModel = await consumerDocument.GetSemanticModelAsync();
    var methodNode = syntaxRoot!.DescendantNodes()
        .OfType<MethodDeclarationSyntax>()
        .Single(method => method.Identifier.ValueText == "Execute");
    var methodSymbol = semanticModel!.GetDeclaredSymbol(methodNode)!;

    var collector = new RoslynOutgoingCallCollector();
    var outgoingCalls = await collector.CollectAsync(
        methodSymbol,
        workspace.CurrentSolution,
        new AnalyzeOptions(),
        CancellationToken.None);

    return outgoingCalls.FirstOrDefault(call => call.ReferenceText == "Run");
}

static ImmutableArray<MetadataReference> GetPlatformReferences()
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

    foreach (var assembly in new[]
             {
                 typeof(object).Assembly,
                 typeof(Enumerable).Assembly,
                 typeof(CSharpCompilation).Assembly,
                 typeof(Workspace).Assembly,
             })
    {
        if (!string.IsNullOrWhiteSpace(assembly.Location))
        {
            references.Add(assembly.Location);
        }
    }

    return references
        .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();
}
