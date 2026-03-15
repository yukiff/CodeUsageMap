namespace CodeUsageMap.Contracts.Graph;

public sealed class GraphNode
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public NodeKind Kind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public string SymbolKey { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}
