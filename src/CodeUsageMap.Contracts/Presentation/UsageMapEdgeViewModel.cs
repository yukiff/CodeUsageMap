using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Presentation
{

public sealed class UsageMapEdgeViewModel
{
    public required string Id { get; init; }

    public required string SourceId { get; init; }

    public required string TargetId { get; init; }

    public EdgeKind Kind { get; init; } = EdgeKind.Reference;

    public string Label { get; init; } = string.Empty;

    public double Confidence { get; init; } = 1.0d;

    public IReadOnlyList<UsageMapDetailItem> Details { get; init; } = System.Array.Empty<UsageMapDetailItem>();
}
}
