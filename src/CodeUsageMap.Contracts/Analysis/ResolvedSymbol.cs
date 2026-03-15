using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Analysis;

public sealed class ResolvedSymbol
{
    public required string DisplayName { get; init; }

    public required string SymbolKey { get; init; }

    public string NamespaceName { get; init; } = string.Empty;

    public string Accessibility { get; init; } = string.Empty;

    public NodeKind Kind { get; init; } = NodeKind.Unknown;
}
