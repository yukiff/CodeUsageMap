using System.Diagnostics.CodeAnalysis;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Core;
using CodeUsageMap.Core.Presentation;

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var representativeSolutionPath = Path.Combine(repositoryRoot, "samples", "RepresentativeSample", "RepresentativeSample.sln");

var analyzer = new CSharpUsageAnalyzer();
var result = await analyzer.AnalyzeAsync(
    new AnalyzeRequest
    {
        SolutionPath = representativeSolutionPath,
        SymbolName = "M:Representative.App.WorkflowRunner.RunAsync",
        Options = new AnalyzeOptions
        {
            WorkspaceLoader = "adhoc",
            Depth = 2,
        },
    },
    CancellationToken.None);

Assert(result.SymbolResolution.Status == SymbolResolutionStatus.Resolved, "PRESENTATION_CONSISTENCY_RESOLUTION_FAILED");

var usageMap = new UsageMapViewModelBuilder().Build(result);
var graphCanvas = new GraphCanvasViewModelBuilder().Build(result);
var assessment = new UsageNodeAssessmentBuilder().Build(usageMap, usageMap.RootNode.Id);

Assert(string.Equals(usageMap.RootNode.Id, "M:Representative.App.WorkflowRunner.RunAsync", StringComparison.Ordinal),
    "PRESENTATION_CONSISTENCY_USAGE_ROOT_MISMATCH");
Assert(string.Equals(graphCanvas.RootNodeId, usageMap.RootNode.Id, StringComparison.Ordinal),
    "PRESENTATION_CONSISTENCY_CANVAS_ROOT_MISMATCH");
Assert(usageMap.Summary.NodeCount == result.Graph.Nodes.Count, "PRESENTATION_CONSISTENCY_NODE_COUNT_MISMATCH");
Assert(usageMap.Summary.EdgeCount == result.Graph.Edges.Count, "PRESENTATION_CONSISTENCY_EDGE_COUNT_MISMATCH");
Assert(usageMap.IncomingRelations.Count + usageMap.OutgoingRelations.Count + usageMap.RelatedRelations.Count == result.Graph.Edges.Count,
    "PRESENTATION_CONSISTENCY_RELATION_COUNT_MISMATCH");
Assert(graphCanvas.Nodes.Count == result.Graph.Nodes.Count, "PRESENTATION_CONSISTENCY_CANVAS_NODE_COUNT_MISMATCH");
Assert(graphCanvas.Edges.Count == result.Graph.Edges.Count, "PRESENTATION_CONSISTENCY_CANVAS_EDGE_COUNT_MISMATCH");
Assert(assessment.Impact.IncomingReferenceCount >= 1, "PRESENTATION_CONSISTENCY_IMPACT_SUMMARY_MISSING");
Assert(!string.IsNullOrWhiteSpace(assessment.Risk.RiskLevel), "PRESENTATION_CONSISTENCY_RISK_SUMMARY_MISSING");

Console.WriteLine("PRESENTATION_CONSISTENCY_PROBE_CONFIRMED");

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
