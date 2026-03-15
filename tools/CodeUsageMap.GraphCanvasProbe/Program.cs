using System.Diagnostics.CodeAnalysis;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;
using CodeUsageMap.Core.Presentation;

var graph = new UsageGraph();

graph.Nodes.Add(CreateNode("root", "OrderService.SaveAsync()", NodeKind.Method));
graph.Nodes.Add(CreateNode("in-1", "OrderController.Post()", NodeKind.Method));
graph.Nodes.Add(CreateNode("in-2", "ApiGateway.Dispatch()", NodeKind.Method));
graph.Nodes.Add(CreateNode("out-1", "OrderRepository.Save()", NodeKind.Method));
graph.Nodes.Add(CreateNode("out-2", "SqlExecutor.Execute()", NodeKind.Method));
graph.Nodes.Add(CreateNode("related-1", "IOrderService", NodeKind.Interface));

graph.Edges.Add(CreateEdge("in-1", "root", EdgeKind.DirectCall));
graph.Edges.Add(CreateEdge("in-2", "in-1", EdgeKind.DirectCall));
graph.Edges.Add(CreateEdge("root", "out-1", EdgeKind.DirectCall));
graph.Edges.Add(CreateEdge("out-1", "out-2", EdgeKind.DirectCall));
graph.Edges.Add(CreateEdge("related-1", "root", EdgeKind.Implements));
graph.Edges.Add(CreateEdge("root", "related-1", EdgeKind.Reference));
graph.Edges.Add(CreateEdge("related-1", "out-1", EdgeKind.Reference));

var result = new AnalysisResult
{
    Graph = graph,
    SymbolResolution = new SymbolResolutionInfo
    {
        Status = SymbolResolutionStatus.Resolved,
        RequestedSymbolName = "OrderService.SaveAsync",
    },
};

var canvas = new GraphCanvasViewModelBuilder().Build(result);

var root = RequireNode(canvas, "root");
var inboundLevel1 = RequireNode(canvas, "in-1");
var inboundLevel2 = RequireNode(canvas, "in-2");
var outboundLevel1 = RequireNode(canvas, "out-1");
var outboundLevel2 = RequireNode(canvas, "out-2");
var related = RequireNode(canvas, "related-1");

Assert(root.Lane == CanvasNodeLane.Center, "ROOT_LANE_MISMATCH");
Assert(root.Depth == 0, "ROOT_DEPTH_MISMATCH");
Assert(root.X == 0d, "ROOT_X_MISMATCH");

Assert(inboundLevel1.Lane == CanvasNodeLane.Inbound, "INBOUND_L1_LANE_MISMATCH");
Assert(inboundLevel1.Depth == 1, "INBOUND_L1_DEPTH_MISMATCH");
Assert(inboundLevel1.X < 0d, "INBOUND_L1_X_MISMATCH");

Assert(inboundLevel2.Lane == CanvasNodeLane.Inbound, "INBOUND_L2_LANE_MISMATCH");
Assert(inboundLevel2.Depth == 2, "INBOUND_L2_DEPTH_MISMATCH");
Assert(inboundLevel2.X < inboundLevel1.X, "INBOUND_L2_X_ORDER_MISMATCH");

Assert(outboundLevel1.Lane == CanvasNodeLane.Outbound, "OUTBOUND_L1_LANE_MISMATCH");
Assert(outboundLevel1.Depth == 1, "OUTBOUND_L1_DEPTH_MISMATCH");
Assert(outboundLevel1.X > 0d, "OUTBOUND_L1_X_MISMATCH");

Assert(outboundLevel2.Lane == CanvasNodeLane.Outbound, "OUTBOUND_L2_LANE_MISMATCH");
Assert(outboundLevel2.Depth == 2, "OUTBOUND_L2_DEPTH_MISMATCH");
Assert(outboundLevel2.X > outboundLevel1.X, "OUTBOUND_L2_X_ORDER_MISMATCH");

Assert(related.Lane == CanvasNodeLane.Related, "RELATED_LANE_MISMATCH");
Assert(related.Y > root.Y, "RELATED_Y_MISMATCH");

var inboundEdge = RequireEdge(canvas, "in-1", "root", EdgeKind.DirectCall);
var outboundEdge = RequireEdge(canvas, "root", "out-1", EdgeKind.DirectCall);
var relatedEdge = RequireEdge(canvas, "related-1", "out-1", EdgeKind.Reference);

Assert(inboundEdge.Lane == CanvasNodeLane.Inbound, "INBOUND_EDGE_LANE_MISMATCH");
Assert(outboundEdge.Lane == CanvasNodeLane.Outbound, "OUTBOUND_EDGE_LANE_MISMATCH");
Assert(relatedEdge.Lane == CanvasNodeLane.Related, "RELATED_EDGE_LANE_MISMATCH");

Console.WriteLine("GRAPH_CANVAS_PROBE_CONFIRMED");

static GraphNode CreateNode(string id, string name, NodeKind kind)
{
    return new GraphNode
    {
        Id = id,
        DisplayName = name,
        Kind = kind,
        ProjectName = "CodeUsageMap.Integration.Tests",
        FilePath = $"/tmp/{id}.cs",
        LineNumber = 1,
        SymbolKey = id,
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["symbolOrigin"] = "source",
        },
    };
}

static GraphEdge CreateEdge(string sourceId, string targetId, EdgeKind kind)
{
    return new GraphEdge
    {
        SourceId = sourceId,
        TargetId = targetId,
        Kind = kind,
        Label = kind.ToString(),
        Confidence = 1.0d,
    };
}

static CanvasNodeViewModel RequireNode(GraphCanvasViewModel canvas, string id)
{
    return canvas.Nodes.FirstOrDefault(node => node.Id == id) ?? FailValue<CanvasNodeViewModel>($"NODE_MISSING_{id}");
}

static CanvasEdgeViewModel RequireEdge(GraphCanvasViewModel canvas, string sourceId, string targetId, EdgeKind kind)
{
    return canvas.Edges.FirstOrDefault(edge =>
               edge.SourceId == sourceId &&
               edge.TargetId == targetId &&
               edge.Kind == kind)
           ?? FailValue<CanvasEdgeViewModel>($"EDGE_MISSING_{sourceId}_{targetId}_{kind}");
}

static void Assert(bool condition, string code)
{
    if (!condition)
    {
        Fail(code);
    }
}

[DoesNotReturn]
static T FailValue<T>(string code)
{
    Console.WriteLine(code);
    Environment.Exit(1);
    throw new InvalidOperationException(code);
}

[DoesNotReturn]
static void Fail(string code)
{
    Console.WriteLine(code);
    Environment.Exit(1);
}
