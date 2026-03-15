using System.Collections.Generic;

namespace CodeUsageMap.Contracts.Graph
{

public sealed class UsageGraph
{
    public List<GraphNode> Nodes { get; } = new List<GraphNode>();

    public List<GraphEdge> Edges { get; } = new List<GraphEdge>();
}
}
