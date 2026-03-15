using CodeUsageMap.Contracts.Presentation;
using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Vsix.ViewModels;

internal sealed class UsageMapCanvasEdgeItemViewModel
{
    public required string Id { get; init; }

    public required string SourceId { get; init; }

    public required string TargetId { get; init; }

    public EdgeKind Kind { get; init; } = EdgeKind.Reference;

    public CanvasNodeLane Lane { get; init; } = CanvasNodeLane.Related;

    public string Stroke { get; init; } = "#8899A1A6";

    public double StrokeThickness { get; init; } = 1.5d;

    public double Opacity { get; set; } = 1d;

    public string PathData { get; init; } = string.Empty;
}
