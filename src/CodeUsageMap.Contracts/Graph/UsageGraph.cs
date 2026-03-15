namespace CodeUsageMap.Contracts.Graph;

public sealed class UsageGraph
{
    public List<GraphNode> Nodes { get; } = [];

    public List<GraphEdge> Edges { get; } = [];
}
