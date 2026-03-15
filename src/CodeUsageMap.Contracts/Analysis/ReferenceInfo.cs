using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Analysis
{

public sealed class ReferenceInfo
{
    public required string ContainingSymbol { get; init; }

    public NodeKind ContainingSymbolKind { get; init; } = NodeKind.Method;

    public required string FilePath { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public string NamespaceName { get; init; } = string.Empty;

    public string Accessibility { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public EdgeKind Kind { get; init; } = EdgeKind.Reference;

    public string SyntaxKind { get; init; } = string.Empty;

    public string ReferenceText { get; init; } = string.Empty;
}
}
