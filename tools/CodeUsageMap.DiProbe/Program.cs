using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core;

var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var solutionPath = Path.Combine(rootPath, "CodeUsageMap.sln");
var analyzer = new CSharpUsageAnalyzer();

var methodResult = await analyzer.AnalyzeAsync(
    new AnalyzeRequest
    {
        SolutionPath = solutionPath,
        SymbolName = "CodeUsageMap.Integration.Tests.DiSamples.IOrderService.SubmitAsync",
        Options = new AnalyzeOptions
        {
            WorkspaceLoader = "adhoc",
            Depth = 1,
        },
    },
    CancellationToken.None);

var methodDiEdge = methodResult.Graph.Edges.FirstOrDefault(edge =>
    edge.Kind == EdgeKind.DiResolvedCall &&
    edge.SourceId.Contains("IOrderService", StringComparison.Ordinal) &&
    edge.TargetId.Contains("OrderService", StringComparison.Ordinal));
var methodRegistrationEdge = methodResult.Graph.Edges.FirstOrDefault(edge =>
    edge.Kind == EdgeKind.InjectedByDi &&
    string.Equals(edge.Label, "Scoped", StringComparison.Ordinal));

if (methodDiEdge is null || methodRegistrationEdge is null)
{
    Console.WriteLine("DI_PROBE_FAILED:METHOD_REGISTRATION_MISSING");
    return;
}

Console.WriteLine($"Method DI edge: {methodDiEdge.SourceId} -> {methodDiEdge.TargetId}");
Console.WriteLine($"Method DI lifetime: {methodDiEdge.Label}");

var typeResult = await analyzer.AnalyzeAsync(
    new AnalyzeRequest
    {
        SolutionPath = solutionPath,
        SymbolName = "CodeUsageMap.Integration.Tests.DiSamples.IReportService",
        Options = new AnalyzeOptions
        {
            WorkspaceLoader = "adhoc",
            Depth = 1,
        },
    },
    CancellationToken.None);

var typeDiEdge = typeResult.Graph.Edges.FirstOrDefault(edge =>
    edge.Kind == EdgeKind.DiResolvedCall &&
    edge.SourceId.Contains("IReportService", StringComparison.Ordinal) &&
    edge.TargetId.Contains("ReportService", StringComparison.Ordinal));
var typeRegistrationNode = typeResult.Graph.Nodes.FirstOrDefault(node =>
    node.Kind == NodeKind.DiRegistration &&
    node.DisplayName.Contains("Transient", StringComparison.Ordinal));

if (typeDiEdge is null || typeRegistrationNode is null)
{
    Console.WriteLine("DI_PROBE_FAILED:TYPEOF_REGISTRATION_MISSING");
    return;
}

Console.WriteLine($"Type DI edge: {typeDiEdge.SourceId} -> {typeDiEdge.TargetId}");
Console.WriteLine($"Type registration: {typeRegistrationNode.DisplayName}");
Console.WriteLine("DI_PROBE_CONFIRMED");
