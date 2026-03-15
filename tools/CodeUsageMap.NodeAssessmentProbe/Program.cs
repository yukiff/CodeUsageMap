using System.Diagnostics.CodeAnalysis;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core.Presentation;

var graph = new UsageGraph();

graph.Nodes.Add(CreateNode("root", "WorkflowBase.ExecuteAsync()", NodeKind.Method, "Representative.Core", "Public", "18"));
graph.Nodes.Add(CreateNode("app-caller", "WorkflowRunner.Run()", NodeKind.Method, "Representative.App", "Public"));
graph.Nodes.Add(CreateNode("test-caller", "WorkflowRunnerTests.Run_executes_workflow()", NodeKind.Method, "Representative.Tests", "Public"));
graph.Nodes.Add(CreateNode("interface-node", "IWorkflow.ExecuteAsync()", NodeKind.Method, "Representative.Core", "Public"));
graph.Nodes.Add(CreateNode("derived-node", "SpecializedWorkflow.ExecuteAsync()", NodeKind.Method, "Representative.Core", "Public"));

graph.Edges.Add(CreateEdge("app-caller", "root", EdgeKind.DirectCall, "Representative.App", "/tmp/Representative.App/WorkflowRunner.cs"));
graph.Edges.Add(CreateEdge("test-caller", "root", EdgeKind.DirectCall, "Representative.Tests", "/tmp/Representative.Tests/WorkflowRunnerTests.cs"));
graph.Edges.Add(CreateEdge("interface-node", "root", EdgeKind.Implements, "Representative.Core", "/tmp/Representative.Core/IWorkflow.cs"));
graph.Edges.Add(CreateEdge("root", "derived-node", EdgeKind.Overrides, "Representative.Core", "/tmp/Representative.Core/SpecializedWorkflow.cs"));

var result = new AnalysisResult
{
    Graph = graph,
    SymbolResolution = new SymbolResolutionInfo
    {
        Status = SymbolResolutionStatus.Resolved,
        RequestedSymbolName = "Representative.Core.WorkflowBase.ExecuteAsync",
    },
};

var usageMap = new UsageMapViewModelBuilder().Build(result);
var assessment = new UsageNodeAssessmentBuilder().Build(usageMap, "root");

Assert(assessment.Impact.ReferencingProjectCount == 3, "IMPACT_PROJECT_COUNT_MISMATCH");
Assert(assessment.Impact.ImplementationCount == 1, "IMPACT_IMPLEMENTATION_COUNT_MISMATCH");
Assert(assessment.Impact.OverrideCount == 1, "IMPACT_OVERRIDE_COUNT_MISMATCH");
Assert(assessment.Impact.HasTestReference, "IMPACT_TEST_REFERENCE_MISMATCH");
Assert(assessment.Impact.IncomingReferenceCount == 3, "IMPACT_INCOMING_REFERENCE_COUNT_MISMATCH");
Assert(assessment.Impact.ComplexityScore == 18, "IMPACT_COMPLEXITY_MISMATCH");

Assert(assessment.Risk.RiskScore == 65, "RISK_SCORE_MISMATCH");
Assert(string.Equals(assessment.Risk.RiskLevel, "Medium", StringComparison.Ordinal), "RISK_LEVEL_MISMATCH");
Assert(assessment.Risk.IsPublicApi, "RISK_PUBLIC_API_MISMATCH");
Assert(assessment.Risk.Drivers.Contains("public API", StringComparer.Ordinal), "RISK_DRIVER_PUBLIC_API_MISSING");
Assert(assessment.Risk.Drivers.Contains("projects 3", StringComparer.Ordinal), "RISK_DRIVER_PROJECTS_MISSING");
Assert(assessment.Risk.Drivers.Contains("tests present", StringComparer.Ordinal), "RISK_DRIVER_TESTS_MISSING");

Console.WriteLine("Impact: projects={0}, implementations={1}, overrides={2}, tests={3}",
    assessment.Impact.ReferencingProjectCount,
    assessment.Impact.ImplementationCount,
    assessment.Impact.OverrideCount,
    assessment.Impact.HasTestReference);
Console.WriteLine("Risk: score={0}, level={1}, publicApi={2}",
    assessment.Risk.RiskScore,
    assessment.Risk.RiskLevel,
    assessment.Risk.IsPublicApi);
Console.WriteLine("NODE_ASSESSMENT_PROBE_CONFIRMED");

static GraphNode CreateNode(
    string id,
    string displayName,
    NodeKind kind,
    string projectName,
    string accessibility,
    string? complexity = null)
{
    var properties = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["symbolOrigin"] = "source",
        ["accessibility"] = accessibility,
        ["namespaceName"] = "Representative",
    };

    if (!string.IsNullOrWhiteSpace(complexity))
    {
        properties["complexity"] = complexity;
    }

    return new GraphNode
    {
        Id = id,
        DisplayName = displayName,
        Kind = kind,
        ProjectName = projectName,
        FilePath = $"/tmp/{projectName}/{id}.cs",
        LineNumber = 1,
        SymbolKey = id,
        Properties = properties,
    };
}

static GraphEdge CreateEdge(
    string sourceId,
    string targetId,
    EdgeKind kind,
    string projectName,
    string filePath)
{
    return new GraphEdge
    {
        SourceId = sourceId,
        TargetId = targetId,
        Kind = kind,
        Label = kind.ToString(),
        Confidence = 1.0d,
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["projectName"] = projectName,
            ["filePath"] = filePath,
        },
    };
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
