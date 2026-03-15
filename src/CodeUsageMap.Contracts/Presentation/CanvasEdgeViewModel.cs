using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Presentation;

public sealed class CanvasEdgeViewModel
{
    public required string Id { get; init; }

    public required string SourceId { get; init; }

    public required string TargetId { get; init; }

    public EdgeKind Kind { get; init; } = EdgeKind.Reference;

    public string Label { get; init; } = string.Empty;

    public double Confidence { get; init; } = 1.0d;

    public CanvasEdgeStyle Style { get; init; } = CanvasEdgeStyle.Solid;

    public CanvasNodeLane Lane { get; init; } = CanvasNodeLane.Related;

    public IReadOnlyList<UsageMapDetailItem> Details { get; init; } = [];
}
