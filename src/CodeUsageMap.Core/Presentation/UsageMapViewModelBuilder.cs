using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;

namespace CodeUsageMap.Core.Presentation;

public sealed class UsageMapViewModelBuilder
{
    public UsageMapViewModel Build(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var rootNode = result.Graph.Nodes.FirstOrDefault() ?? CreateResolutionPlaceholder(result.SymbolResolution);

        var nodeMap = result.Graph.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var nodes = result.Graph.Nodes
            .Select(node => ToNodeViewModel(node, node.Id == rootNode.Id))
            .ToArray();
        var edges = result.Graph.Edges
            .Select(ToEdgeViewModel)
            .ToArray();

        var incoming = new List<UsageMapRelationViewModel>();
        var outgoing = new List<UsageMapRelationViewModel>();
        var related = new List<UsageMapRelationViewModel>();

        foreach (var edge in result.Graph.Edges)
        {
            var direction = edge.TargetId == rootNode.Id
                ? UsageMapRelationDirection.Incoming
                : edge.SourceId == rootNode.Id
                    ? UsageMapRelationDirection.Outgoing
                    : UsageMapRelationDirection.Related;

            var relation = ToRelationViewModel(edge, direction, nodeMap);
            switch (direction)
            {
                case UsageMapRelationDirection.Incoming:
                    incoming.Add(relation);
                    break;
                case UsageMapRelationDirection.Outgoing:
                    outgoing.Add(relation);
                    break;
                default:
                    related.Add(relation);
                    break;
            }
        }

        return new UsageMapViewModel
        {
            Title = rootNode.DisplayName,
            RootNode = ToNodeViewModel(rootNode, isRoot: true),
            SymbolResolution = ToSymbolResolutionViewModel(result.SymbolResolution),
            Summary = new UsageMapSummaryViewModel
            {
                NodeCount = nodes.Length,
                EdgeCount = edges.Length,
                IncomingCount = incoming.Count,
                OutgoingCount = outgoing.Count,
                RelatedCount = related.Count,
                DiagnosticCount = result.Diagnostics.Count,
            },
            Nodes = nodes,
            Edges = edges,
            IncomingRelations = incoming,
            OutgoingRelations = outgoing,
            RelatedRelations = related,
            Diagnostics = result.Diagnostics,
        };
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

    private static UsageMapNodeViewModel ToNodeViewModel(GraphNode node, bool isRoot)
    {
        return new UsageMapNodeViewModel
        {
            Id = node.Id,
            DisplayName = node.DisplayName,
            Kind = node.Kind,
            ProjectName = node.ProjectName,
            NamespaceName = ResolveNamespaceName(node),
            Accessibility = ResolveAccessibility(node),
            FilePath = node.FilePath,
            LineNumber = node.LineNumber,
            SymbolKey = node.SymbolKey,
            IsRoot = isRoot,
            IsExternal = IsExternal(node),
            ExternalCategory = ResolveExternalCategory(node),
            Details = ToDetails(node.Properties),
        };
    }

    private static UsageMapEdgeViewModel ToEdgeViewModel(GraphEdge edge)
    {
        return new UsageMapEdgeViewModel
        {
            Id = CreateEdgeId(edge),
            SourceId = edge.SourceId,
            TargetId = edge.TargetId,
            Kind = edge.Kind,
            Label = edge.Label,
            Confidence = edge.Confidence,
            Details = ToDetails(edge.Properties),
        };
    }

    private static UsageMapRelationViewModel ToRelationViewModel(
        GraphEdge edge,
        UsageMapRelationDirection direction,
        IReadOnlyDictionary<string, GraphNode> nodeMap)
    {
        var sourceNode = nodeMap.TryGetValue(edge.SourceId, out var source) ? source : CreateFallbackNode(edge.SourceId);
        var targetNode = nodeMap.TryGetValue(edge.TargetId, out var target) ? target : CreateFallbackNode(edge.TargetId);

        return new UsageMapRelationViewModel
        {
            EdgeId = CreateEdgeId(edge),
            Direction = direction,
            SourceNodeId = sourceNode.Id,
            SourceDisplayName = sourceNode.DisplayName,
            SourceKind = sourceNode.Kind,
            TargetNodeId = targetNode.Id,
            TargetDisplayName = targetNode.DisplayName,
            TargetKind = targetNode.Kind,
            ProjectName = ResolveProjectName(edge, sourceNode, targetNode),
            NamespaceName = ResolveNamespaceName(edge, sourceNode, targetNode),
            Accessibility = ResolveAccessibility(edge, sourceNode, targetNode),
            IsExternal = IsExternal(edge, sourceNode, targetNode),
            ExternalCategory = ResolveExternalCategory(edge, sourceNode, targetNode),
            EdgeKind = edge.Kind,
            Label = edge.Label,
            Confidence = edge.Confidence,
            Details = ToDetails(edge.Properties),
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

    private static string CreateEdgeId(GraphEdge edge)
    {
        return $"{edge.SourceId}|{edge.TargetId}|{edge.Kind}|{edge.Label}";
    }

    private static string ResolveProjectName(GraphEdge edge, GraphNode sourceNode, GraphNode targetNode)
    {
        if (edge.Properties.TryGetValue("projectName", out var projectName) && !string.IsNullOrWhiteSpace(projectName))
        {
            return projectName;
        }

        if (!string.IsNullOrWhiteSpace(sourceNode.ProjectName))
        {
            return sourceNode.ProjectName;
        }

        return targetNode.ProjectName;
    }

    private static string ResolveNamespaceName(GraphNode node)
    {
        if (node.Properties.TryGetValue("namespaceName", out var namespaceName) && !string.IsNullOrWhiteSpace(namespaceName))
        {
            return namespaceName;
        }

        return string.Empty;
    }

    private static string ResolveNamespaceName(GraphEdge edge, GraphNode sourceNode, GraphNode targetNode)
    {
        if (edge.Properties.TryGetValue("namespaceName", out var namespaceName) && !string.IsNullOrWhiteSpace(namespaceName))
        {
            return namespaceName;
        }

        var sourceNamespace = ResolveNamespaceName(sourceNode);
        if (!string.IsNullOrWhiteSpace(sourceNamespace))
        {
            return sourceNamespace;
        }

        return ResolveNamespaceName(targetNode);
    }

    private static string ResolveAccessibility(GraphNode node)
    {
        if (node.Properties.TryGetValue("accessibility", out var accessibility) && !string.IsNullOrWhiteSpace(accessibility))
        {
            return accessibility;
        }

        return string.Empty;
    }

    private static string ResolveAccessibility(GraphEdge edge, GraphNode sourceNode, GraphNode targetNode)
    {
        if (edge.Properties.TryGetValue("accessibility", out var accessibility) && !string.IsNullOrWhiteSpace(accessibility))
        {
            return accessibility;
        }

        var sourceAccessibility = ResolveAccessibility(sourceNode);
        var targetAccessibility = ResolveAccessibility(targetNode);

        return string.IsNullOrWhiteSpace(sourceAccessibility)
            ? targetAccessibility
            : string.IsNullOrWhiteSpace(targetAccessibility) || string.Equals(sourceAccessibility, targetAccessibility, StringComparison.Ordinal)
                ? sourceAccessibility
                : $"{sourceAccessibility}|{targetAccessibility}";
    }

    private static bool IsExternal(GraphNode node)
    {
        return node.Properties.TryGetValue("symbolOrigin", out var symbolOrigin) &&
            string.Equals(symbolOrigin, "metadata", StringComparison.Ordinal);
    }

    private static bool IsExternal(GraphEdge edge, GraphNode sourceNode, GraphNode targetNode)
    {
        if (edge.Properties.TryGetValue("symbolOrigin", out var symbolOrigin) &&
            string.Equals(symbolOrigin, "metadata", StringComparison.Ordinal))
        {
            return true;
        }

        return IsExternal(sourceNode) || IsExternal(targetNode);
    }

    private static string ResolveExternalCategory(GraphNode node)
    {
        if (!IsExternal(node))
        {
            return string.Empty;
        }

        return ClassifyExternalCategory(
            GetProperty(node.Properties, "assemblyIdentity"),
            node.ProjectName,
            node.DisplayName);
    }

    private static string ResolveExternalCategory(GraphEdge edge, GraphNode sourceNode, GraphNode targetNode)
    {
        if (!IsExternal(edge, sourceNode, targetNode))
        {
            return string.Empty;
        }

        var assemblyIdentity = GetProperty(edge.Properties, "assemblyIdentity");
        var projectName = ResolveProjectName(edge, sourceNode, targetNode);
        var displayName = string.IsNullOrWhiteSpace(targetNode.DisplayName) ? sourceNode.DisplayName : targetNode.DisplayName;
        return ClassifyExternalCategory(assemblyIdentity, projectName, displayName);
    }

    private static string ClassifyExternalCategory(string assemblyIdentity, string projectName, string displayName)
    {
        var name = FirstNonEmpty(assemblyIdentity, projectName, displayName);
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

    private static string GetProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static UsageMapSymbolResolutionViewModel ToSymbolResolutionViewModel(SymbolResolutionInfo resolution)
    {
        return new UsageMapSymbolResolutionViewModel
        {
            Status = resolution.Status,
            RequestedSymbolName = resolution.RequestedSymbolName,
            RequestedSymbolIndex = resolution.RequestedSymbolIndex,
            SelectedSymbolIndex = resolution.SelectedSymbolIndex,
            Candidates = resolution.Candidates
                .Select(static candidate => new UsageMapSymbolCandidateViewModel
                {
                    Index = candidate.Index,
                    DisplayName = candidate.DisplayName,
                    ProjectName = candidate.ProjectName,
                    MatchKind = candidate.MatchKind,
                    FilePath = candidate.FilePath,
                    LineNumber = candidate.LineNumber,
                    Kind = candidate.Kind,
                })
                .ToArray(),
        };
    }
}
