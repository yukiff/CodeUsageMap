using System.Globalization;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;
using CodeUsageMap.Core.Compatibility;

namespace CodeUsageMap.Core.Presentation
{

public sealed class UsageNodeAssessmentBuilder
{
    public UsageNodeAssessmentViewModel Build(UsageMapViewModel model, string nodeId)
    {
        Guard.NotNull(model, nameof(model));
        Guard.NotNullOrWhiteSpace(nodeId, nameof(nodeId));

        var node = model.Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        if (node is null)
        {
            return new UsageNodeAssessmentViewModel();
        }

        var relations = model.IncomingRelations
            .Concat(model.OutgoingRelations)
            .Concat(model.RelatedRelations)
            .ToArray();
        var inboundRelations = relations
            .Where(relation => string.Equals(relation.TargetNodeId, nodeId, StringComparison.Ordinal))
            .ToArray();
        var relatedInboundRelations = relations
            .Where(relation =>
                relation.Direction == UsageMapRelationDirection.Related &&
                string.Equals(relation.TargetNodeId, nodeId, StringComparison.Ordinal))
            .ToArray();
        var allInboundRelations = inboundRelations.Concat(relatedInboundRelations).ToArray();

        var impact = new UsageImpactSummaryViewModel
        {
            ReferencingProjectCount = allInboundRelations
                .Select(static relation => relation.ProjectName)
                .Where(static projectName => !string.IsNullOrWhiteSpace(projectName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            ImplementationCount = relations.Count(relation =>
                relation.EdgeKind == EdgeKind.Implements &&
                (string.Equals(relation.SourceNodeId, nodeId, StringComparison.Ordinal) ||
                 string.Equals(relation.TargetNodeId, nodeId, StringComparison.Ordinal))),
            OverrideCount = relations.Count(relation =>
                relation.EdgeKind == EdgeKind.Overrides &&
                (string.Equals(relation.SourceNodeId, nodeId, StringComparison.Ordinal) ||
                 string.Equals(relation.TargetNodeId, nodeId, StringComparison.Ordinal))),
            HasTestReference = allInboundRelations.Any(LooksLikeTestReference),
            IncomingReferenceCount = inboundRelations.Length,
            ComplexityScore = ParseComplexityScore(node),
        };

        return new UsageNodeAssessmentViewModel
        {
            Impact = impact,
            Risk = BuildRiskSummary(node, impact),
        };
    }

    private static UsageRiskSummaryViewModel BuildRiskSummary(
        UsageMapNodeViewModel node,
        UsageImpactSummaryViewModel impact)
    {
        var isPublicApi = IsPublicApi(node.Accessibility);
        var score = 0;
        var drivers = new List<string>();

        if (isPublicApi)
        {
            score += 25;
            drivers.Add("public API");
        }

        if (impact.IncomingReferenceCount > 0)
        {
            score += Math.Min(20, impact.IncomingReferenceCount * 2);
            drivers.Add($"incoming {impact.IncomingReferenceCount}");
        }

        if (impact.ReferencingProjectCount > 0)
        {
            score += Math.Min(25, 10 + ((impact.ReferencingProjectCount - 1) * 5));
            drivers.Add($"projects {impact.ReferencingProjectCount}");
        }

        if (impact.ImplementationCount > 0)
        {
            score += Math.Min(16, impact.ImplementationCount * 8);
            drivers.Add($"implementations {impact.ImplementationCount}");
        }

        if (impact.OverrideCount > 0)
        {
            score += Math.Min(18, impact.OverrideCount * 6);
            drivers.Add($"overrides {impact.OverrideCount}");
        }

        if (impact.ComplexityScore >= 20)
        {
            score += 20;
            drivers.Add($"complexity {impact.ComplexityScore}");
        }
        else if (impact.ComplexityScore >= 10)
        {
            score += 10;
            drivers.Add($"complexity {impact.ComplexityScore}");
        }

        if (impact.HasTestReference)
        {
            score = Math.Max(0, score - 10);
            drivers.Add("tests present");
        }

        score = Math.Min(100, score);

        return new UsageRiskSummaryViewModel
        {
            RiskScore = score,
            RiskLevel = score switch
            {
                >= 75 => "High",
                >= 45 => "Medium",
                _ => "Low",
            },
            IsPublicApi = isPublicApi,
            Drivers = drivers,
        };
    }

    private static bool LooksLikeTestReference(UsageMapRelationViewModel relation)
    {
        if (ContainsTestMarker(relation.ProjectName))
        {
            return true;
        }

        return relation.Details.Any(detail =>
            string.Equals(detail.Key, "filePath", StringComparison.OrdinalIgnoreCase) &&
            ContainsTestMarker(detail.Value));
    }

    private static bool ContainsTestMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("spec", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsPublicApi(string accessibility)
    {
        if (string.IsNullOrWhiteSpace(accessibility))
        {
            return false;
        }

        return accessibility.IndexOf("Public", StringComparison.OrdinalIgnoreCase) >= 0 ||
               accessibility.IndexOf("Protected", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int ParseComplexityScore(UsageMapNodeViewModel node)
    {
        var complexityText = node.Details
            .FirstOrDefault(detail => string.Equals(detail.Key, "complexity", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        if (string.IsNullOrWhiteSpace(complexityText))
        {
            return 0;
        }

        return int.TryParse(complexityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var complexity)
            ? complexity
            : 0;
    }
}
}
