using System.Collections.Generic;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;

namespace CodeUsageMap.Vsix.ViewModels
{

internal sealed class UsageMapRelationItemViewModel
{
    public required string EdgeId { get; init; }

    public UsageMapRelationDirection Direction { get; init; } = UsageMapRelationDirection.Related;

    public required string SourceNodeId { get; init; }

    public required string SourceDisplayName { get; init; }

    public NodeKind SourceKind { get; init; } = NodeKind.Unknown;

    public required string TargetNodeId { get; init; }

    public required string TargetDisplayName { get; init; }

    public NodeKind TargetKind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string NamespaceName { get; init; } = string.Empty;

    public string Accessibility { get; init; } = string.Empty;

    public bool IsExternal { get; init; }

    public string ExternalCategory { get; init; } = string.Empty;

    public EdgeKind EdgeKind { get; init; } = EdgeKind.Reference;

    public string Label { get; init; } = string.Empty;

    public double Confidence { get; init; } = 1.0d;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public double Opacity { get; set; } = 1d;

    public IReadOnlyList<UsageMapDetailItemViewModel> Details { get; init; } = System.Array.Empty<UsageMapDetailItemViewModel>();
}
}
