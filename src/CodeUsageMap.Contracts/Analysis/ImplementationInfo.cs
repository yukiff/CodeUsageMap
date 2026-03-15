using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Analysis;

public sealed class ImplementationInfo
{
    public required string DisplayName { get; init; }

    public required string SymbolKey { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public string NamespaceName { get; init; } = string.Empty;

    public string Accessibility { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public NodeKind NodeKind { get; init; } = NodeKind.Unknown;

    public EdgeKind Kind { get; init; } = EdgeKind.Implements;

    public string ContainingTypeName { get; init; } = string.Empty;

    public bool IsOverride { get; init; }
}
