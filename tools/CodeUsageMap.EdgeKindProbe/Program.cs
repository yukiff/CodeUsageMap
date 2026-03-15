using System.Diagnostics.CodeAnalysis;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core;

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var repositorySolutionPath = Path.Combine(repositoryRoot, "CodeUsageMap.sln");
var representativeSolutionPath = Path.Combine(repositoryRoot, "samples", "RepresentativeSample", "RepresentativeSample.sln");

var analyzer = new CSharpUsageAnalyzer();
var observedKinds = new HashSet<EdgeKind>();

await CollectKindsAsync(analyzer, repositorySolutionPath, "M:CodeUsageMap.Integration.Tests.Samples.IProcessor.Run", observedKinds);
await CollectKindsAsync(analyzer, repositorySolutionPath, "T:CodeUsageMap.Integration.Tests.Samples.Processor", observedKinds);
await CollectKindsAsync(analyzer, repositorySolutionPath, "E:CodeUsageMap.Integration.Tests.Samples.SamplePublisher.WorkCompleted", observedKinds);
await CollectKindsAsync(analyzer, repositorySolutionPath, "M:CodeUsageMap.Integration.Tests.OutgoingSamples.DynamicPipeline.Execute", observedKinds);
await CollectKindsAsync(analyzer, representativeSolutionPath, "M:Representative.Core.IWorkflow.ExecuteAsync", observedKinds);
await CollectKindsAsync(analyzer, representativeSolutionPath, "M:Representative.Core.WorkflowBase.ExecuteAsync", observedKinds);

var expectedKinds = new[]
{
    EdgeKind.DirectCall,
    EdgeKind.InterfaceDispatch,
    EdgeKind.DiResolvedCall,
    EdgeKind.Reference,
    EdgeKind.Implements,
    EdgeKind.Overrides,
    EdgeKind.InjectedByDi,
    EdgeKind.InstantiatedBy,
    EdgeKind.ContainsSubscription,
    EdgeKind.EventSubscription,
    EdgeKind.EventUnsubscription,
    EdgeKind.EventHandlerTarget,
    EdgeKind.EventRaise,
    EdgeKind.EventDispatchEstimated,
    EdgeKind.UnknownDynamicDispatch,
};

var missingKinds = expectedKinds
    .Where(kind => !observedKinds.Contains(kind))
    .ToArray();

Assert(missingKinds.Length == 0,
    $"EDGE_KIND_PROBE_MISSING:{string.Join(",", missingKinds.Select(static kind => kind.ToString()))}");

Console.WriteLine("EDGE_KIND_PROBE_CONFIRMED");

static async Task CollectKindsAsync(
    CSharpUsageAnalyzer analyzer,
    string solutionPath,
    string symbolName,
    ISet<EdgeKind> observedKinds)
{
    var result = await analyzer.AnalyzeAsync(
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

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, $"EDGE_KIND_PROBE_RESOLUTION_FAILED:{symbolName}");

    foreach (var edge in result.Graph.Edges)
    {
        observedKinds.Add(edge.Kind);
    }
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
