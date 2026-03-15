using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;

namespace CodeUsageMap.Core.Presentation;

public sealed class GraphCanvasViewModelBuilder
{
    private const double HorizontalSpacing = 280d;
    private const double VerticalSpacing = 104d;
    private const double RelatedHorizontalSpacing = 160d;
    private const double RelatedTopOffset = 180d;

    public GraphCanvasViewModel Build(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var usageMap = new UsageMapViewModelBuilder().Build(result);
        var rootNode = result.Graph.Nodes.FirstOrDefault(node => node.Id == usageMap.RootNode.Id)
            ?? result.Graph.Nodes.FirstOrDefault()
            ?? CreateResolutionPlaceholder(result.SymbolResolution);
        var rootNodeId = rootNode.Id;

        var nodeMap = result.Graph.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var outgoing = BuildAdjacency(result.Graph.Edges, static edge => edge.SourceId, static edge => edge.TargetId);
        var incoming = BuildAdjacency(result.Graph.Edges, static edge => edge.TargetId, static edge => edge.SourceId);
        var undirected = BuildUndirectedAdjacency(result.Graph.Edges);

        var inboundDepths = ComputeDistances(rootNodeId, incoming);
        var outboundDepths = ComputeDistances(rootNodeId, outgoing);
        var undirectedDepths = ComputeDistances(rootNodeId, undirected);

        var placements = BuildPlacements(result.Graph.Nodes, rootNodeId, incoming, outgoing, inboundDepths, outboundDepths, undirectedDepths);
        var placementMap = placements.ToDictionary(static placement => placement.Node.Id, StringComparer.Ordinal);

        var nodes = placements
            .Select(static placement => ToCanvasNode(placement.Node, placement))
            .ToArray();

        var edges = result.Graph.Edges
            .Select(edge => ToCanvasEdge(edge, placementMap, rootNodeId))
            .ToArray();

        return new GraphCanvasViewModel
        {
            Title = usageMap.Title,
            DisplayMode = GraphCanvasDisplayMode.CallMap,
            RootNodeId = rootNodeId,
            Nodes = nodes,
            Edges = edges,
            Summary = usageMap.Summary,
            SymbolResolution = usageMap.SymbolResolution,
            Diagnostics = result.Diagnostics,
        };
    }

    private static IReadOnlyList<NodePlacement> BuildPlacements(
        IReadOnlyList<GraphNode> nodes,
        string rootNodeId,
        IReadOnlyDictionary<string, List<string>> incoming,
        IReadOnlyDictionary<string, List<string>> outgoing,
        IReadOnlyDictionary<string, int> inboundDepths,
        IReadOnlyDictionary<string, int> outboundDepths,
        IReadOnlyDictionary<string, int> undirectedDepths)
    {
        var laneStates = nodes
            .Select(node => DetermineLaneState(node, rootNodeId, inboundDepths, outboundDepths, undirectedDepths))
            .ToDictionary(static state => state.Node.Id, StringComparer.Ordinal);

        var orderMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [rootNodeId] = 0,
        };

        var placements = new List<NodePlacement>(nodes.Count);
        var rootNode = laneStates[rootNodeId].Node;
        placements.Add(new NodePlacement(rootNode, CanvasNodeLane.Center, 0, 0, 0d, 0d));

        AddLanePlacements(
            placements,
            orderMap,
            laneStates.Values.Where(static state => state.Lane == CanvasNodeLane.Inbound).ToArray(),
            CanvasNodeLane.Inbound,
            incoming,
            outgoing,
            isInbound: true);

        AddLanePlacements(
            placements,
            orderMap,
            laneStates.Values.Where(static state => state.Lane == CanvasNodeLane.Outbound).ToArray(),
            CanvasNodeLane.Outbound,
            incoming,
            outgoing,
            isInbound: false);

        AddRelatedPlacements(
            placements,
            orderMap,
            laneStates.Values.Where(static state => state.Lane == CanvasNodeLane.Related).ToArray());

        return placements;
    }

    private static LaneState DetermineLaneState(
        GraphNode node,
        string rootNodeId,
        IReadOnlyDictionary<string, int> inboundDepths,
        IReadOnlyDictionary<string, int> outboundDepths,
        IReadOnlyDictionary<string, int> undirectedDepths)
    {
        if (string.Equals(node.Id, rootNodeId, StringComparison.Ordinal))
        {
            return new LaneState(node, CanvasNodeLane.Center, 0);
        }

        var hasInbound = inboundDepths.TryGetValue(node.Id, out var inboundDepth);
        var hasOutbound = outboundDepths.TryGetValue(node.Id, out var outboundDepth);

        if (hasInbound && !hasOutbound)
        {
            return new LaneState(node, CanvasNodeLane.Inbound, inboundDepth);
        }

        if (!hasInbound && hasOutbound)
        {
            return new LaneState(node, CanvasNodeLane.Outbound, outboundDepth);
        }

        if (hasInbound && hasOutbound)
        {
            if (inboundDepth < outboundDepth)
            {
                return new LaneState(node, CanvasNodeLane.Inbound, inboundDepth);
            }

            if (outboundDepth < inboundDepth)
            {
                return new LaneState(node, CanvasNodeLane.Outbound, outboundDepth);
            }
        }

        var relatedDepth = undirectedDepths.TryGetValue(node.Id, out var distance)
            ? Math.Max(1, distance)
            : 1;

        return new LaneState(node, CanvasNodeLane.Related, relatedDepth);
    }

    private static void AddLanePlacements(
        ICollection<NodePlacement> placements,
        Dictionary<string, int> orderMap,
        IReadOnlyList<LaneState> laneStates,
        CanvasNodeLane lane,
        IReadOnlyDictionary<string, List<string>> incoming,
        IReadOnlyDictionary<string, List<string>> outgoing,
        bool isInbound)
    {
        if (laneStates.Count == 0)
        {
            return;
        }

        foreach (var depthGroup in laneStates.GroupBy(static state => state.Depth).OrderBy(static group => group.Key))
        {
            var sortedStates = depthGroup
                .OrderBy(state => ResolvePrimaryOrder(state, orderMap, incoming, outgoing, isInbound))
                .ThenBy(state => ResolveSecondaryOrder(state, orderMap, incoming, outgoing, isInbound))
                .ThenBy(static state => state.Node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var startY = -((sortedStates.Length - 1) * VerticalSpacing) / 2d;
            for (var index = 0; index < sortedStates.Length; index++)
            {
                var state = sortedStates[index];
                var y = startY + (index * VerticalSpacing);
                var x = lane == CanvasNodeLane.Inbound
                    ? -(state.Depth * HorizontalSpacing)
                    : state.Depth * HorizontalSpacing;

                orderMap[state.Node.Id] = index;
                placements.Add(new NodePlacement(state.Node, lane, state.Depth, index, x, y));
            }
        }
    }

    private static void AddRelatedPlacements(
        ICollection<NodePlacement> placements,
        Dictionary<string, int> orderMap,
        IReadOnlyList<LaneState> laneStates)
    {
        if (laneStates.Count == 0)
        {
            return;
        }

        foreach (var depthGroup in laneStates.GroupBy(static state => state.Depth).OrderBy(static group => group.Key))
        {
            var sortedStates = depthGroup
                .OrderBy(static state => state.Node.ProjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static state => state.Node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var startY = RelatedTopOffset + (((depthGroup.Key - 1) * VerticalSpacing) / 2d);
            for (var index = 0; index < sortedStates.Length; index++)
            {
                var state = sortedStates[index];
                var x = (depthGroup.Key - 1) * RelatedHorizontalSpacing;
                var y = startY + (index * VerticalSpacing);
                orderMap[state.Node.Id] = index;
                placements.Add(new NodePlacement(state.Node, CanvasNodeLane.Related, state.Depth, index, x, y));
            }
        }
    }

    private static double ResolvePrimaryOrder(
        LaneState state,
        IReadOnlyDictionary<string, int> orderMap,
        IReadOnlyDictionary<string, List<string>> incoming,
        IReadOnlyDictionary<string, List<string>> outgoing,
        bool isInbound)
    {
        var neighbors = isInbound
            ? GetOrEmpty(outgoing, state.Node.Id)
            : GetOrEmpty(incoming, state.Node.Id);

        var parentOrders = neighbors
            .Select(neighborId => orderMap.TryGetValue(neighborId, out var order) ? order : (int?)null)
            .Where(static order => order.HasValue)
            .Select(static order => order!.Value)
            .ToArray();

        if (parentOrders.Length == 0)
        {
            return double.MaxValue;
        }

        return parentOrders.Average();
    }

    private static int ResolveSecondaryOrder(
        LaneState state,
        IReadOnlyDictionary<string, int> orderMap,
        IReadOnlyDictionary<string, List<string>> incoming,
        IReadOnlyDictionary<string, List<string>> outgoing,
        bool isInbound)
    {
        var neighbors = isInbound
            ? GetOrEmpty(outgoing, state.Node.Id)
            : GetOrEmpty(incoming, state.Node.Id);

        foreach (var neighborId in neighbors)
        {
            if (orderMap.TryGetValue(neighborId, out var order))
            {
                return order;
            }
        }

        return int.MaxValue;
    }

    private static CanvasNodeViewModel ToCanvasNode(GraphNode node, NodePlacement placement)
    {
        return new CanvasNodeViewModel
        {
            Id = node.Id,
            DisplayName = node.DisplayName,
            Kind = node.Kind,
            ProjectName = node.ProjectName,
            FilePath = node.FilePath,
            LineNumber = node.LineNumber,
            SymbolKey = node.SymbolKey,
            IsRoot = placement.Lane == CanvasNodeLane.Center,
            IsExternal = string.Equals(GetProperty(node.Properties, "symbolOrigin"), "metadata", StringComparison.Ordinal),
            ExternalCategory = ResolveExternalCategory(node),
            Lane = placement.Lane,
            Depth = placement.Depth,
            Order = placement.Order,
            X = placement.X,
            Y = placement.Y,
            Width = placement.Lane == CanvasNodeLane.Center ? 260d : 220d,
            Height = placement.Lane == CanvasNodeLane.Center ? 84d : 72d,
            Accent = ResolveAccent(node),
            Details = ToDetails(node.Properties),
        };
    }

    private static CanvasEdgeViewModel ToCanvasEdge(
        GraphEdge edge,
        IReadOnlyDictionary<string, NodePlacement> placementMap,
        string rootNodeId)
    {
        var sourcePlacement = placementMap.TryGetValue(edge.SourceId, out var source)
            ? source
            : new NodePlacement(CreateFallbackNode(edge.SourceId), CanvasNodeLane.Related, 1, 0, 0d, RelatedTopOffset);
        var targetPlacement = placementMap.TryGetValue(edge.TargetId, out var target)
            ? target
            : new NodePlacement(CreateFallbackNode(edge.TargetId), CanvasNodeLane.Related, 1, 0, 0d, RelatedTopOffset);

        return new CanvasEdgeViewModel
        {
            Id = CreateEdgeId(edge),
            SourceId = edge.SourceId,
            TargetId = edge.TargetId,
            Kind = edge.Kind,
            Label = edge.Label,
            Confidence = edge.Confidence,
            Style = ResolveStyle(edge),
            Lane = ResolveEdgeLane(edge, sourcePlacement, targetPlacement, rootNodeId),
            Details = ToDetails(edge.Properties),
        };
    }

    private static CanvasNodeLane ResolveEdgeLane(
        GraphEdge edge,
        NodePlacement sourcePlacement,
        NodePlacement targetPlacement,
        string rootNodeId)
    {
        if (edge.TargetId == rootNodeId)
        {
            return CanvasNodeLane.Inbound;
        }

        if (edge.SourceId == rootNodeId)
        {
            return CanvasNodeLane.Outbound;
        }

        if (sourcePlacement.Lane == targetPlacement.Lane)
        {
            return sourcePlacement.Lane;
        }

        if (sourcePlacement.Lane == CanvasNodeLane.Center)
        {
            return targetPlacement.Lane;
        }

        if (targetPlacement.Lane == CanvasNodeLane.Center)
        {
            return sourcePlacement.Lane;
        }

        return CanvasNodeLane.Related;
    }

    private static CanvasEdgeStyle ResolveStyle(GraphEdge edge)
    {
        return edge.Kind switch
        {
            EdgeKind.Reference => CanvasEdgeStyle.Dashed,
            EdgeKind.EventDispatchEstimated or EdgeKind.UnknownDynamicDispatch => CanvasEdgeStyle.Dotted,
            EdgeKind.Implements or EdgeKind.Overrides => CanvasEdgeStyle.Bold,
            _ => CanvasEdgeStyle.Solid,
        };
    }

    private static string ResolveAccent(GraphNode node)
    {
        return node.Kind switch
        {
            NodeKind.Class => "class",
            NodeKind.Interface => "interface",
            NodeKind.Method => "method",
            NodeKind.Property => "property",
            NodeKind.Event => "event",
            _ => "default",
        };
    }

    private static string ResolveExternalCategory(GraphNode node)
    {
        if (!string.Equals(GetProperty(node.Properties, "symbolOrigin"), "metadata", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var name = FirstNonEmpty(
            GetProperty(node.Properties, "assemblyIdentity"),
            node.ProjectName,
            node.DisplayName);

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Package";
        }

        return name.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase)
            ? "Framework"
            : "Package";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static IReadOnlyDictionary<string, List<string>> BuildAdjacency(
        IReadOnlyList<GraphEdge> edges,
        Func<GraphEdge, string> keySelector,
        Func<GraphEdge, string> valueSelector)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            var key = keySelector(edge);
            if (!map.TryGetValue(key, out var list))
            {
                list = [];
                map[key] = list;
            }

            var value = valueSelector(edge);
            if (!list.Contains(value, StringComparer.Ordinal))
            {
                list.Add(value);
            }
        }

        return map;
    }

    private static IReadOnlyDictionary<string, List<string>> BuildUndirectedAdjacency(IReadOnlyList<GraphEdge> edges)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            AddNeighbor(map, edge.SourceId, edge.TargetId);
            AddNeighbor(map, edge.TargetId, edge.SourceId);
        }

        return map;
    }

    private static IReadOnlyDictionary<string, int> ComputeDistances(
        string rootNodeId,
        IReadOnlyDictionary<string, List<string>> adjacency)
    {
        var distances = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [rootNodeId] = 0,
        };
        var queue = new Queue<string>();
        queue.Enqueue(rootNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors)
            {
                if (distances.ContainsKey(neighbor))
                {
                    continue;
                }

                distances[neighbor] = distances[current] + 1;
                queue.Enqueue(neighbor);
            }
        }

        return distances;
    }

    private static IReadOnlyList<UsageMapDetailItem> ToDetails(IReadOnlyDictionary<string, string> properties)
    {
        return properties
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(static item => new UsageMapDetailItem
            {
                Key = item.Key,
                Value = item.Value,
            })
            .ToArray();
    }

    private static IReadOnlyList<string> GetOrEmpty(
        IReadOnlyDictionary<string, List<string>> map,
        string key)
    {
        return map.TryGetValue(key, out var values) ? values : [];
    }

    private static void AddNeighbor(
        IDictionary<string, List<string>> adjacency,
        string key,
        string neighbor)
    {
        if (!adjacency.TryGetValue(key, out var list))
        {
            list = [];
            adjacency[key] = list;
        }

        if (!list.Contains(neighbor, StringComparer.Ordinal))
        {
            list.Add(neighbor);
        }
    }

    private static string CreateEdgeId(GraphEdge edge)
    {
        return $"{edge.SourceId}|{edge.TargetId}|{edge.Kind}|{edge.Label}";
    }

    private static string GetProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static GraphNode CreateResolutionPlaceholder(SymbolResolutionInfo resolution)
    {
        var displayName = string.IsNullOrWhiteSpace(resolution.RequestedSymbolName)
            ? "Unresolved symbol"
            : resolution.RequestedSymbolName;

        return new GraphNode
        {
            Id = resolution.RequestedSymbolName,
            DisplayName = displayName,
            Kind = NodeKind.Unknown,
            SymbolKey = resolution.RequestedSymbolName,
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["symbolResolutionStatus"] = resolution.Status.ToString(),
                ["requestedSymbolName"] = resolution.RequestedSymbolName,
            },
        };
    }

    private static GraphNode CreateFallbackNode(string nodeId)
    {
        return new GraphNode
        {
            Id = nodeId,
            DisplayName = nodeId,
            Kind = NodeKind.Unknown,
            SymbolKey = nodeId,
        };
    }

    private sealed record LaneState(GraphNode Node, CanvasNodeLane Lane, int Depth);

    private sealed record NodePlacement(
        GraphNode Node,
        CanvasNodeLane Lane,
        int Depth,
        int Order,
        double X,
        double Y);
}
