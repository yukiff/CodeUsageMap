using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Presentation
{

public sealed class UsageMapRelationViewModel
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

    public IReadOnlyList<UsageMapDetailItem> Details { get; init; } = System.Array.Empty<UsageMapDetailItem>();
}
}
