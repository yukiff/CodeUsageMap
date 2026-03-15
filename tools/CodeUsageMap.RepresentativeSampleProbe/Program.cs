using System.Diagnostics.CodeAnalysis;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core;

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var solutionPath = Path.Combine(repositoryRoot, "samples", "RepresentativeSample", "RepresentativeSample.sln");

var analyzer = new CSharpUsageAnalyzer();

await VerifyAppAndTestFlowAsync(analyzer, solutionPath);
await VerifyDiFlowAsync(analyzer, solutionPath);
await VerifyEventFlowAsync(analyzer, solutionPath);
await VerifyClassRootAsync(analyzer, solutionPath);
await VerifyPropertyRootAsync(analyzer, solutionPath);
await VerifyOverrideFlowAsync(analyzer, solutionPath);

Console.WriteLine("REPRESENTATIVE_SAMPLE_PROBE_CONFIRMED");

static async Task VerifyAppAndTestFlowAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var result = await AnalyzeAsync(analyzer, solutionPath, "M:Representative.App.WorkflowRunner.RunAsync");
    var nodeMap = result.Graph.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "REPRESENTATIVE_APP_FLOW_RESOLUTION_FAILED");
    Assert(HasProjectInboundEdge(result, "M:Representative.App.WorkflowRunner.RunAsync", "Representative.Tests"),
        "REPRESENTATIVE_TEST_REFERENCE_MISSING");
}

static async Task VerifyDiFlowAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var result = await AnalyzeAsync(analyzer, solutionPath, "M:Representative.Core.IWorkflow.ExecuteAsync");
    var nodeMap = result.Graph.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "REPRESENTATIVE_DI_RESOLUTION_FAILED");
    Assert(HasEdgeFromDisplayNameToTargetId(result, nodeMap, EdgeKind.DirectCall, "Representative.App.WorkflowRunner.RunAsync()", "M:Representative.Core.IWorkflow.ExecuteAsync"),
        "REPRESENTATIVE_DIRECT_CALL_TO_INTERFACE_MISSING");
    Assert(HasAnyOutgoingEdge(result, EdgeKind.Implements, "M:Representative.Core.IWorkflow.ExecuteAsync"),
        "REPRESENTATIVE_IMPLEMENTS_EDGE_MISSING");
    Assert(HasEdge(result, nodeMap, EdgeKind.DiResolvedCall, "M:Representative.Core.IWorkflow.ExecuteAsync", "M:Representative.Core.DefaultWorkflow.ExecuteAsync"),
        "REPRESENTATIVE_DI_EDGE_MISSING");
}

static async Task VerifyEventFlowAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var subscriptionResult = await AnalyzeAsync(
        analyzer,
        solutionPath,
        "M:Representative.App.WorkflowRunner.#ctor(Representative.Core.IWorkflow,Representative.Core.WorkflowObserver)");
    Assert(subscriptionResult.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "REPRESENTATIVE_EVENT_SUBSCRIPTION_RESOLUTION_FAILED");
    Assert(subscriptionResult.Graph.Edges.Any(edge => edge.Kind == EdgeKind.EventSubscription),
        "REPRESENTATIVE_EVENT_SUBSCRIPTION_MISSING");

    var unsubscriptionResult = await AnalyzeAsync(
        analyzer,
        solutionPath,
        "M:Representative.App.WorkflowRunner.StopObserving");
    Assert(unsubscriptionResult.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "REPRESENTATIVE_EVENT_UNSUBSCRIPTION_RESOLUTION_FAILED");
    Assert(unsubscriptionResult.Graph.Edges.Any(edge => edge.Kind == EdgeKind.EventUnsubscription),
        "REPRESENTATIVE_EVENT_UNSUBSCRIPTION_MISSING");

    var raiseResult = await AnalyzeAsync(analyzer, solutionPath, "M:Representative.Core.WorkflowBase.RaiseCompleted");
    Assert(raiseResult.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "REPRESENTATIVE_RAISE_RESOLUTION_FAILED");
    Assert(raiseResult.Graph.Edges.Any(edge => edge.Kind == EdgeKind.EventRaise),
        "REPRESENTATIVE_EVENT_RAISE_MISSING");
}

static async Task VerifyClassRootAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var result = await AnalyzeAsync(analyzer, solutionPath, "T:Representative.App.WorkflowRunner");

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "REPRESENTATIVE_CLASS_ROOT_RESOLUTION_FAILED");
    Assert(result.Graph.Nodes.Any(static node => string.Equals(node.Id, "T:Representative.App.WorkflowRunner", StringComparison.Ordinal)),
        "REPRESENTATIVE_CLASS_ROOT_NODE_MISSING");
    Assert(result.Graph.Edges.Any(static edge => edge.Kind is EdgeKind.Reference or EdgeKind.InstantiatedBy),
        "REPRESENTATIVE_CLASS_ROOT_REFERENCE_MISSING");
}

static async Task VerifyPropertyRootAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var result = await AnalyzeAsync(analyzer, solutionPath, "P:Representative.Core.WorkflowObserver.CompletionCount");

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "REPRESENTATIVE_PROPERTY_ROOT_RESOLUTION_FAILED");
    Assert(result.Graph.Edges.Any(edge =>
            edge.Kind == EdgeKind.Reference &&
            string.Equals(edge.TargetId, "P:Representative.Core.WorkflowObserver.CompletionCount", StringComparison.Ordinal) &&
            edge.SourceId.Contains("WorkflowObserver.OnCompleted", StringComparison.Ordinal)),
        "REPRESENTATIVE_PROPERTY_REFERENCE_MISSING");
}

static async Task VerifyOverrideFlowAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var result = await AnalyzeAsync(analyzer, solutionPath, "M:Representative.Core.WorkflowBase.ExecuteAsync");

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "REPRESENTATIVE_OVERRIDE_RESOLUTION_FAILED");
    Assert(result.Graph.Edges.Any(edge =>
            edge.Kind == EdgeKind.Overrides &&
            string.Equals(edge.SourceId, "M:Representative.Core.WorkflowBase.ExecuteAsync", StringComparison.Ordinal) &&
            string.Equals(edge.TargetId, "M:Representative.Core.DefaultWorkflow.ExecuteAsync", StringComparison.Ordinal)),
        "REPRESENTATIVE_OVERRIDE_EDGE_MISSING");
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

static bool HasEdge(
    AnalysisResult result,
    IReadOnlyDictionary<string, GraphNode> nodeMap,
    EdgeKind kind,
    string sourceId,
    string targetId)
{
    return result.Graph.Edges.Any(edge =>
        edge.Kind == kind &&
        string.Equals(edge.SourceId, sourceId, StringComparison.Ordinal) &&
        string.Equals(edge.TargetId, targetId, StringComparison.Ordinal) &&
        nodeMap.ContainsKey(edge.SourceId) &&
        nodeMap.ContainsKey(edge.TargetId));
}

static bool HasEdgeFromDisplayNameToTargetId(
    AnalysisResult result,
    IReadOnlyDictionary<string, GraphNode> nodeMap,
    EdgeKind kind,
    string sourceDisplayName,
    string targetId)
{
    return result.Graph.Edges.Any(edge =>
        edge.Kind == kind &&
        string.Equals(edge.TargetId, targetId, StringComparison.Ordinal) &&
        nodeMap.TryGetValue(edge.SourceId, out var sourceNode) &&
        string.Equals(sourceNode.DisplayName, sourceDisplayName, StringComparison.Ordinal));
}

static bool HasProjectInboundEdge(
    AnalysisResult result,
    string targetId,
    string projectName)
{
    return result.Graph.Edges.Any(edge =>
        string.Equals(edge.TargetId, targetId, StringComparison.Ordinal) &&
        edge.Properties.TryGetValue("projectName", out var edgeProjectName) &&
        string.Equals(edgeProjectName, projectName, StringComparison.Ordinal));
}

static bool HasAnyOutgoingEdge(AnalysisResult result, EdgeKind kind, string sourceId)
{
    return result.Graph.Edges.Any(edge =>
        edge.Kind == kind &&
        string.Equals(edge.SourceId, sourceId, StringComparison.Ordinal));
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
