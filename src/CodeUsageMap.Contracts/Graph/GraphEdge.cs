namespace CodeUsageMap.Contracts.Graph
{

public sealed class GraphEdge
{
    public required string SourceId { get; init; }

    public required string TargetId { get; init; }

    public EdgeKind Kind { get; init; } = EdgeKind.Reference;

    public string Label { get; init; } = string.Empty;

    public double Confidence { get; init; } = 1.0d;

    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}
}
