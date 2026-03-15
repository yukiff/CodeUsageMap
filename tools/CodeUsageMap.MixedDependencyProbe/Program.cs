using System.Diagnostics.CodeAnalysis;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core;

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var solutionPath = Path.Combine(repositoryRoot, "samples", "MixedDependencySample", "MixedDependencySample.sln");

var analyzer = new CSharpUsageAnalyzer();

await VerifyBuildRunnerAsync(analyzer, solutionPath);
await VerifyNotifyFanInAsync(analyzer, solutionPath);
await VerifyEventFlowAsync(analyzer, solutionPath);

Console.WriteLine("MIXED_DEPENDENCY_PROBE_CONFIRMED");

static async Task VerifyBuildRunnerAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var result = await AnalyzeAsync(analyzer, solutionPath, "M:Mixed.App.Bootstrapper.BuildRunner");
    var projects = result.Graph.Nodes
        .Select(static node => node.ProjectName)
        .Where(static name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "MIXED_BUILD_RUNNER_RESOLUTION_FAILED");
    Assert(projects.Contains("Mixed.App", StringComparer.Ordinal), "MIXED_BUILD_RUNNER_APP_PROJECT_MISSING");
    Assert(projects.Contains("Mixed.Core", StringComparer.Ordinal), "MIXED_BUILD_RUNNER_CORE_PROJECT_MISSING");
    Assert(projects.Contains("Mixed.Infrastructure", StringComparer.Ordinal), "MIXED_BUILD_RUNNER_INFRA_PROJECT_MISSING");
    Assert(result.Graph.Edges.Any(static edge => edge.Kind == EdgeKind.InstantiatedBy), "MIXED_BUILD_RUNNER_INSTANTIATION_MISSING");
    Assert(result.Graph.Edges.Any(static edge => edge.Kind == EdgeKind.DirectCall), "MIXED_BUILD_RUNNER_DIRECT_CALL_MISSING");
}

static async Task VerifyNotifyFanInAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var result = await AnalyzeAsync(analyzer, solutionPath, "Mixed.Core.OrderService.NotifyAsync");

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "MIXED_NOTIFY_RESOLUTION_FAILED");
    Assert(HasInboundProject(result, "M:Mixed.Core.OrderService.NotifyAsync(Mixed.Abstractions.OrderRecord)", "Mixed.Core"),
        "MIXED_NOTIFY_CORE_INBOUND_MISSING");
    Assert(HasInboundProject(result, "M:Mixed.Core.OrderService.NotifyAsync(Mixed.Abstractions.OrderRecord)", "Mixed.Tests"),
        "MIXED_NOTIFY_TEST_INBOUND_MISSING");
}

static async Task VerifyEventFlowAsync(CSharpUsageAnalyzer analyzer, string solutionPath)
{
    var result = await AnalyzeAsync(analyzer, solutionPath, "E:Mixed.Core.OrderService.Submitted");

    Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "MIXED_EVENT_RESOLUTION_FAILED");
    Assert(result.Graph.Edges.Any(static edge => edge.Kind == EdgeKind.EventSubscription), "MIXED_EVENT_SUBSCRIPTION_MISSING");
    Assert(result.Graph.Edges.Any(static edge => edge.Kind == EdgeKind.EventUnsubscription), "MIXED_EVENT_UNSUBSCRIPTION_MISSING");
    Assert(result.Graph.Nodes.Any(node => string.Equals(node.ProjectName, "Mixed.Infrastructure", StringComparison.Ordinal)),
        "MIXED_EVENT_INFRA_NODE_MISSING");
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

static bool HasInboundProject(AnalysisResult result, string targetId, string projectName)
{
    return result.Graph.Edges.Any(edge =>
        string.Equals(edge.TargetId, targetId, StringComparison.Ordinal) &&
        edge.Properties.TryGetValue("projectName", out var edgeProjectName) &&
        string.Equals(edgeProjectName, projectName, StringComparison.Ordinal));
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
