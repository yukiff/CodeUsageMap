using CodeUsageMap.Contracts.Presentation;
using CodeUsageMap.Core.Compatibility;

namespace CodeUsageMap.Core.Presentation
{

public sealed class UsageMapViewModelFilter
{
    public UsageMapViewModel Apply(UsageMapViewModel model, UsageMapFilterCriteria criteria)
    {
        Guard.NotNull(model, nameof(model));
        Guard.NotNull(criteria, nameof(criteria));

        var incoming = model.IncomingRelations.Where(relation => Matches(relation, criteria)).ToArray();
        var outgoing = model.OutgoingRelations.Where(relation => Matches(relation, criteria)).ToArray();
        var related = model.RelatedRelations.Where(relation => Matches(relation, criteria)).ToArray();
        var visibleEdgeIds = incoming
            .Concat(outgoing)
            .Concat(related)
            .Select(static relation => relation.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        var visibleNodeIds = incoming
            .Concat(outgoing)
            .Concat(related)
            .SelectMany(static relation => new[] { relation.SourceNodeId, relation.TargetNodeId })
            .Append(model.RootNode.Id)
            .ToHashSet(StringComparer.Ordinal);

        var nodes = model.Nodes
            .Where(node => visibleNodeIds.Contains(node.Id))
            .ToArray();
        var edges = model.Edges
            .Where(edge => visibleEdgeIds.Contains(edge.Id))
            .ToArray();

        return new UsageMapViewModel
        {
            Title = model.Title,
            RootNode = model.RootNode,
            Summary = new UsageMapSummaryViewModel
            {
                NodeCount = nodes.Length,
                EdgeCount = edges.Length,
                IncomingCount = incoming.Length,
                OutgoingCount = outgoing.Length,
                RelatedCount = related.Length,
                DiagnosticCount = model.Diagnostics.Count,
            },
            Nodes = nodes,
            Edges = edges,
            IncomingRelations = incoming,
            OutgoingRelations = outgoing,
            RelatedRelations = related,
            Diagnostics = model.Diagnostics,
        };
    }

    private static bool Matches(UsageMapRelationViewModel relation, UsageMapFilterCriteria criteria)
    {
        if (criteria.EdgeKind is not null && relation.EdgeKind != criteria.EdgeKind.Value)
        {
            return false;
        }

        if (criteria.NodeKind is not null &&
            relation.SourceKind != criteria.NodeKind.Value &&
            relation.TargetKind != criteria.NodeKind.Value)
        {
            return false;
        }

        if (criteria.MinimumConfidence is not null && relation.Confidence < criteria.MinimumConfidence.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.ProjectName) &&
            !string.Equals(relation.ProjectName, criteria.ProjectName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.NamespaceName) &&
            !Contains(relation.NamespaceName, criteria.NamespaceName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.Accessibility) &&
            !Contains(relation.Accessibility, criteria.Accessibility))
        {
            return false;
        }

        if (criteria.ExcludeExternalSymbols && relation.IsExternal)
        {
            return false;
        }

        if (criteria.ExcludeSystemSymbols &&
            relation.IsExternal &&
            string.Equals(relation.ExternalCategory, "Framework", StringComparison.Ordinal))
        {
            return false;
        }

        if (criteria.ExcludePackageSymbols &&
            relation.IsExternal &&
            string.Equals(relation.ExternalCategory, "Package", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            return true;
        }

        return Contains(relation.SourceDisplayName, criteria.SearchText) ||
               Contains(relation.TargetDisplayName, criteria.SearchText) ||
               Contains(relation.Label, criteria.SearchText) ||
               Contains(relation.ProjectName, criteria.SearchText) ||
               relation.Details.Any(detail => Contains(detail.Key, criteria.SearchText) || Contains(detail.Value, criteria.SearchText));
    }

    private static bool Contains(string value, string searchText)
    {
        return value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
}
