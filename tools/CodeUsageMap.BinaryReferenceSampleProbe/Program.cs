using System.Diagnostics.CodeAnalysis;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core;

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var solutionPath = Path.Combine(repositoryRoot, "samples", "BinaryReferenceSample", "BinaryReferenceSample.sln");

var analyzer = new CSharpUsageAnalyzer();

await VerifyBinaryReferenceNormalizationAsync(analyzer, solutionPath);

Console.WriteLine("BINARY_REFERENCE_SAMPLE_PROBE_CONFIRMED");

static async Task VerifyBinaryReferenceNormalizationAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var result = await AnalyzeAsync(analyzer, solutionPath, "M:Binary.Consumer.ConsumerEntry.Execute");

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "BINARY_REFERENCE_RESOLUTION_FAILED");
    Assert(result.Graph.Nodes.Any(static node => string.Equals(node.ProjectName, "Binary.Consumer", StringComparison.Ordinal)),
        "BINARY_REFERENCE_CONSUMER_PROJECT_MISSING");
    Assert(result.Graph.Nodes.Any(static node => string.Equals(node.ProjectName, "Binary.SourceLib", StringComparison.Ordinal)),
        "BINARY_REFERENCE_SOURCE_PROJECT_MISSING");
    Assert(result.Graph.Edges.Any(edge =>
            edge.Kind == EdgeKind.DirectCall &&
            edge.Properties.TryGetValue("normalizedFromMetadata", out var normalizedFromMetadata) &&
            string.Equals(normalizedFromMetadata, "true", StringComparison.OrdinalIgnoreCase) &&
            edge.Properties.TryGetValue("projectName", out var projectName) &&
            string.Equals(projectName, "Binary.SourceLib", StringComparison.Ordinal)),
        "BINARY_REFERENCE_NORMALIZED_DIRECT_CALL_MISSING");
    Assert(result.Graph.Edges.Any(edge =>
            edge.Kind == EdgeKind.InstantiatedBy &&
            edge.Properties.TryGetValue("projectName", out var projectName) &&
            string.Equals(projectName, "Binary.SourceLib", StringComparison.Ordinal)),
        "BINARY_REFERENCE_SOURCE_INSTANTIATION_MISSING");
}

static async Task<AnalysisResult> AnalyzeAsync(CSharpUsageAnalyzer analyzer, string solutionPath, string symbolName)
{
    return await analyzer.AnalyzeAsync(
        new AnalyzeRequest
        {
            SolutionPath = solutionPath,
            SymbolName = symbolName,
            Options = new AnalyzeOptions
            {
                WorkspaceLoader = "adhoc",
                Depth = 2,
            },
        },
        CancellationToken.None);
}

static void Assert(bool condition, string code)
{
    if (!condition)
    {
        Fail(code);
    }
}

[DoesNotReturn]
static void Fail(string code)
{
    Console.WriteLine(code);
    Environment.Exit(1);
}
